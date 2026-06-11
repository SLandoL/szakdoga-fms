#!/usr/bin/env python
"""
PiCar line follower and MQTT route controller.

The physical StopLeft, StopRight and ResetPos switches are published by the
existing tank ESP. ResetPos is handled as an edge-triggered debug event: every
received switch transition makes the factory the next logical stop.
"""

import time
import traceback
from enum import Enum

import paho.mqtt.client as mqtt
import picar
from SunFounder_Line_Follower import Line_Follower
from picar import back_wheels
from picar import front_wheels


MQTT_BROKER = "192.168.0.100"
MQTT_PORT = 1883
MQTT_KEEPALIVE_SECONDS = 60

REFERENCES = [400, 400, 330, 400, 400]
CALIBRATE = False
FORWARD_SPEED = 0
LOOP_DELAY_SECONDS = 0.0005
HEARTBEAT_INTERVAL_SECONDS = 1.0
FORCE_NEXT_STOP_FACTORY_EVENT = "ForceNextStopFactory"


class States(Enum):
    Container = 1
    Container_to_Factory = 2
    Factory = 3
    Factory_to_Container = 4


# Runtime state. Hardware and MQTT objects are initialized before the MQTT
# network loop starts, preventing callbacks from accessing missing objects.
c = None
fw = None
bw = None
lf = None

state = States.Factory_to_Container
carSpeed = 0.6
carCanGoTank = True
carCanGoBottle = True
carCanGoContainer = True
carStop = False
deadLine = False
StopContainer_to_Factory = True
StopFactory_to_Container = True
color = "blue"
lt_status_now = [0, 0, 0, 0, 0]
turning_angle = 40
last_heartbeat_time = 0.0


def on_connect(client, userdata, flags, rc):
    if rc == 0:
        client.subscribe("carManagement")
        client.subscribe("StopRight")
        client.subscribe("StopLeft")
        client.subscribe("ResetPos")
        print("Connected to MQTT broker:", MQTT_BROKER)
    else:
        print("MQTT connection failed, rc =", rc)


def on_disconnect(client, userdata, rc):
    print("MQTT disconnected, rc =", rc)


def on_msg(client, userdata, msg):
    """Protect the Paho network thread from handler and payload errors."""
    try:
        payload = msg.payload.decode("utf-8").strip()

        print("MQTT received")
        print("Topic:", repr(msg.topic))
        print("Payload:", repr(payload))
        print("carStop:", carStop)
        print("state:", state)

        handle_message(msg.topic, payload)
    except Exception as ex:
        print("MQTT callback error:", repr(ex))
        traceback.print_exc()


def handle_message(topic, payload):
    global StopContainer_to_Factory
    global StopFactory_to_Container

    if topic == "StopRight":
        value = parse_bool_payload(payload, topic)
        if value is not None:
            StopContainer_to_Factory = value
            print("StopContainer_to_Factory =", value)
        return

    if topic == "StopLeft":
        value = parse_bool_payload(payload, topic)
        if value is not None:
            StopFactory_to_Container = value
            print("StopFactory_to_Container =", value)
        return

    if topic == "ResetPos":
        # The tank ESP publishes ResetPos only when the physical input changes.
        # Both edges therefore mean the same one-shot debug event.
        value = parse_bool_payload(payload, topic)
        if value is not None:
            force_next_stop_factory()
        return

    if topic == "carManagement":
        handle_car_management(payload)
        return

    print("Unhandled MQTT topic:", repr(topic))


def parse_bool_payload(payload, topic):
    if payload == "True":
        return True
    if payload == "False":
        return False

    print("Invalid boolean payload on", repr(topic) + ":", repr(payload))
    return None


def handle_car_management(payload):
    global carSpeed
    global color
    global carCanGoContainer
    global carStop
    global deadLine
    global lt_status_now

    parts = [part.strip() for part in payload.split(",")]
    command = parts[0] if parts else ""

    if command == FORCE_NEXT_STOP_FACTORY_EVENT:
        force_next_stop_factory()
        return

    if command == "carSpeed":
        if len(parts) < 2:
            print("Invalid carSpeed command:", repr(payload))
            return

        try:
            speed_percent = int(parts[1])
        except ValueError:
            print("Invalid carSpeed value:", repr(parts[1]))
            return

        speed_percent = max(0, min(100, speed_percent))
        carSpeed = speed_percent / 100.0
        bw.speed = speed_percent
        print("The carSpeed is:", carSpeed)
        return

    if command == "Paused":
        if len(parts) < 2 or parts[1] not in ("True", "False"):
            print("Invalid Paused command:", repr(payload))
            return

        paused = parts[1] == "True"
        if paused:
            c.publish("car-esp", "stop")
            bw.speed = 0
            print("The car is paused.")
        else:
            c.publish("car-esp", "start")
            bw.speed = int(100 * carSpeed)
            carStop = False
            print("The car is unpaused.")
        return

    if command == "WakeUp":
        c.publish("car-esp", "start")
        bw.speed = int(100 * carSpeed)
        carStop = False
        print("Wake up command processed.")
        return

    if command == "carLedColor":
        if len(parts) < 2 or not parts[1]:
            print("Invalid carLedColor command:", repr(payload))
            return

        color = parts[1]
        print("The car's LED color is:", color)
        return

    if command == "CarGOTank":
        handle_tank_ready()
        return

    if command == "CarGOBottle":
        handle_bottle_ready()
        return

    if command == "CarGOContainer":
        c.publish("car-esp", "start")
        bw.speed = int(100 * carSpeed)
        carCanGoContainer = True
        carStop = False
        c.publish("CarLocation", "onTheWayToFactory")

        lt_status_now = lf.read_digital()
        deadLine = lt_status_now == [1, 1, 1, 1, 1]

        print("CarGOContainer received; restarting toward factory")
        return

    print("Unhandled carManagement command:", repr(payload))


def handle_tank_ready():
    global carCanGoTank

    print("CarGOTank received")
    carCanGoTank = True
    print_ready_flags()
    try_continue_to_container()


def handle_bottle_ready():
    global carCanGoBottle

    print("CarGOBottle received")
    carCanGoBottle = True
    print_ready_flags()
    try_continue_to_container()


def print_ready_flags():
    print(
        "Flags:",
        "tank =", carCanGoTank,
        "bottle =", carCanGoBottle,
    )


def try_continue_to_container():
    global carStop
    global deadLine
    global lt_status_now

    if not (carCanGoTank and carCanGoBottle):
        print(
            "Still waiting:",
            "tank =", carCanGoTank,
            "bottle =", carCanGoBottle,
        )
        return

    print("Both stations ready, restarting car")
    result = c.publish("car-esp", "start")
    print("Publishing car-esp start, rc =", result.rc)

    bw.speed = int(100 * carSpeed)
    c.publish("CarLocation", "onTheWayToContainer")

    lt_status_now = lf.read_digital()
    deadLine = lt_status_now == [1, 1, 1, 1, 1]
    carStop = False


def force_next_stop_factory():
    global state

    # In the existing state machine, States.Container means that the next full
    # stop marker is processed as the factory stop.
    state = States.Container
    c.publish("CarLocation", "onTheWayToFactory")
    print("Debug switch event: next stop forced to factory")


def initialize_hardware():
    global fw
    global bw
    global lf
    global state

    picar.setup()

    fw = front_wheels.Front_Wheels(
        db="/home/pi/SunFounder_PiCar-S/example/config"
    )
    bw = back_wheels.Back_Wheels(
        db="/home/pi/SunFounder_PiCar-S/example/config"
    )
    lf = Line_Follower.Line_Follower()

    lf.references = REFERENCES
    fw.ready()
    bw.ready()
    fw.turning_max = 45
    state = States.Factory_to_Container

    print("PiCar hardware initialized")


def connect_mqtt():
    global c

    c = mqtt.Client()
    c.on_connect = on_connect
    c.on_message = on_msg
    c.on_disconnect = on_disconnect

    while True:
        try:
            c.connect(
                MQTT_BROKER,
                MQTT_PORT,
                keepalive=MQTT_KEEPALIVE_SECONDS,
            )
            c.loop_start()
            return
        except Exception as ex:
            print("MQTT connection failed:", repr(ex))
            time.sleep(5)


def publish_heartbeat_if_due():
    global last_heartbeat_time

    now = time.monotonic()
    if now - last_heartbeat_time < HEARTBEAT_INTERVAL_SECONDS:
        return

    result = c.publish("MQTTState", "ONLINE")
    if result.rc != mqtt.MQTT_ERR_SUCCESS:
        print("Heartbeat publish failed, rc =", result.rc)

    last_heartbeat_time = now


def wait_with_heartbeat(duration_seconds):
    end_time = time.monotonic() + duration_seconds

    while True:
        remaining = end_time - time.monotonic()
        if remaining <= 0:
            return

        publish_heartbeat_if_due()
        time.sleep(min(0.1, remaining))


def setup():
    if CALIBRATE:
        cali()


def main():
    global turning_angle
    global carCanGoBottle
    global carCanGoTank
    global carCanGoContainer
    global carStop
    global lt_status_now
    global deadLine
    global state

    bw.speed = int(FORWARD_SPEED * carSpeed)

    a_step = 3
    b_step = 17
    c_step = 27
    d_step = 37

    bw.forward()

    while True:
        lt_status_now = lf.read_digital()

        if deadLine and lt_status_now != [1, 1, 1, 1, 1]:
            deadLine = False

        if lt_status_now == [0, 0, 1, 0, 0]:
            step = 0
        elif lt_status_now in ([0, 1, 1, 0, 0], [0, 0, 1, 1, 0]):
            step = a_step
        elif lt_status_now in ([0, 1, 0, 0, 0], [0, 0, 0, 1, 0]):
            step = b_step
        elif lt_status_now in ([1, 1, 0, 0, 0], [0, 0, 0, 1, 1]):
            step = c_step
        elif lt_status_now in ([1, 0, 0, 0, 0], [0, 0, 0, 0, 1]):
            step = d_step
        elif lt_status_now == [1, 1, 1, 1, 1] and not deadLine:
            handle_stop_marker()

        if lt_status_now == [0, 0, 1, 0, 0]:
            fw.turn(90)
        elif lt_status_now in (
            [0, 1, 1, 0, 0],
            [0, 1, 0, 0, 0],
            [1, 1, 0, 0, 0],
            [1, 0, 0, 0, 0],
        ):
            turning_angle = int(90 - step)
        elif lt_status_now in (
            [0, 0, 1, 1, 0],
            [0, 0, 0, 1, 0],
            [0, 0, 0, 1, 1],
            [0, 0, 0, 0, 1],
        ):
            turning_angle = int(90 + step)

        fw.turn(turning_angle)
        time.sleep(LOOP_DELAY_SECONDS)
        publish_heartbeat_if_due()


def handle_stop_marker():
    global carCanGoTank
    global carCanGoBottle
    global carCanGoContainer
    global carStop
    global deadLine
    global state

    bw.speed = 0
    carStop = True
    deadLine = True

    if not (carCanGoBottle and carCanGoTank and carCanGoContainer):
        print_ready_flags()
        return

    c.publish("car-esp", "stop")

    if state == States.Container:
        c.publish("CarLocation", "factory")
        c.publish("tank-esp", "empty " + color)
        c.publish("car-esp", "fill " + color)
        carCanGoTank = False
        carCanGoBottle = False
        state = States.Container_to_Factory
        print("Factory stop reached; waiting for CarGOTank and CarGOBottle")
        return

    if state == States.Container_to_Factory:
        if StopContainer_to_Factory:
            print("Container-to-factory extra stop enabled")
            wait_with_heartbeat(5.0)

        restart_after_intermediate_stop()
        state = States.Factory
        return

    if state == States.Factory:
        c.publish("CarLocation", "container")
        c.publish("car-esp", "empty " + color)
        carCanGoContainer = False
        state = States.Factory_to_Container
        print("Container stop reached; waiting for CarGOContainer")
        return

    if state == States.Factory_to_Container:
        if StopFactory_to_Container:
            print("Factory-to-container extra stop enabled")
            wait_with_heartbeat(5.0)

        restart_after_intermediate_stop()
        state = States.Container


def restart_after_intermediate_stop():
    global carStop

    bw.speed = int(100 * carSpeed)
    bw.forward()
    time.sleep(0.2)
    carStop = False


def cali():
    references = [0, 0, 0, 0, 0]
    print(
        "cali for module:\n"
        "  first put all sensors on white, then put all sensors on black"
    )
    mount = 100

    fw.turn(70)
    print("\n cali white")
    time.sleep(4)
    fw.turn(90)
    white_references = lf.get_average(mount)
    fw.turn(95)
    time.sleep(0.5)
    fw.turn(85)
    time.sleep(0.5)
    fw.turn(90)
    time.sleep(1)

    fw.turn(110)
    print("\n cali black")
    time.sleep(4)
    fw.turn(90)
    black_references = lf.get_average(mount)
    fw.turn(95)
    time.sleep(0.5)
    fw.turn(85)
    time.sleep(0.5)
    fw.turn(90)
    time.sleep(1)

    for i in range(5):
        references[i] = (white_references[i] + black_references[i]) / 2

    lf.references = references
    print("Middle references =", references)
    time.sleep(1)


def destroy():
    if bw is not None:
        bw.stop()
    if fw is not None:
        fw.turn(90)


def shutdown_mqtt():
    if c is None:
        return

    try:
        c.loop_stop()
        c.disconnect()
    except Exception as ex:
        print("MQTT shutdown error:", repr(ex))


if __name__ == "__main__":
    try:
        initialize_hardware()
        connect_mqtt()
        setup()
        main()
    except KeyboardInterrupt:
        print("Interrupted by user")
    except Exception as ex:
        print("Fatal car controller error:", repr(ex))
        traceback.print_exc()
    finally:
        destroy()
        shutdown_mqtt()

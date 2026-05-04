#!/usr/bin/env python
'''
**********************************************************************
* Filename    : line_follower
* Description : An example for sensor car kit to followe line
* Author      : Dream
* Brand       : SunFounder
* E-mail      : service@sunfounder.com
* Website     : www.sunfounder.com
* Update      : Dream    2016-09-21    New release
**********************************************************************
'''

from SunFounder_Line_Follower import Line_Follower
from picar import front_wheels
from picar import back_wheels
import time
import picar
import paho.mqtt.client as mqtt
import requests
import RPi.GPIO as gpio
from enum import Enum

class States(Enum):
    Container = 1
    Container_to_Factory = 2
    Factory = 3
    Factory_to_Container = 4

def on_connect(client, userdata, flag, rc):
    if(rc == 0): 
        client.subscribe("carManagement")
        client.subscribe("StopRight")
        client.subscribe("StopLeft")
        client.subscribe("ResetPos")
        print("Connected")
    else:
        print("DISCONNECTED")

carSpeed = 0.6
carCanGoTank = True
carCanGoBottle = True
carCanGoContainer = True
carStop = False
deadLine = False
StopContainer_to_Factory = True
StopFactory_to_Container = True
color = "blue"
def on_msg(client, userdata, msg):
    print("New MQTT msg")
    global StopContainer_to_Factory
    global StopFactory_to_Container
    global state
    global carSpeed
    global color
    global carCanGoBottle
    global carCanGoTank
    global carCanGoContainer
    global carStop
    global lt_status_now
    global lf
    global deadLine
    if (msg.topic == "StopRight"):
        if(str(msg.payload.decode("utf-8")) == "True"):
            print("StopRight True")
            StopContainer_to_Factory = True
        if(str(msg.payload.decode("utf-8")) == "False"):
            print("StopRight Fasle")
            StopContainer_to_Factory = False

    if (msg.topic == "StopLeft"):
        if(str(msg.payload.decode("utf-8")) == "True"):
            print("StopLeft True")
            StopFactory_to_Container = True
        if(str(msg.payload.decode("utf-8")) == "False"):
            print("StopLeft Fasle")
            StopFactory_to_Container = False
    
    if (msg.topic == "ResetPos"):
        if(str(msg.payload.decode("utf-8")) == "True"):
            print("Resetpos to container")
            state = States.Container
        if(str(msg.payload.decode("utf-8")) == "False"):
            print("Resetpos to factory")
            state = States.Factory
        
    if (msg.topic == "carManagement"):
        if (carStop == False):
            if (str(msg.payload.decode("utf-8")).split(",")[0] == "carSpeed"):
                carSpeed = int(str(msg.payload.decode("utf-8")).split(",")[1])/100
                bw.speed = int(100*carSpeed)
                print("The carSpeed is: " + str(carSpeed))

            elif (str(msg.payload.decode("utf-8")).split(",")[0] == "WakeUp"):
                c.publish("car-esp",  "start")
                bw.speed = int(100 * carSpeed)
                print("Wake up pls!!!")
                
            elif (str(msg.payload.decode("utf-8")).split(",")[0] == "Paused"):
                if(str(msg.payload.decode("utf-8")).split(",")[1] == "True"):
                    c.publish("car-esp",  "stop")
                    bw.speed = 0
                    print("The car is paused.")
                    
                elif(str(msg.payload.decode("utf-8")).split(",")[1] == "False"):
                    c.publish("car-esp",  "start")
                    bw.speed = int(100 * carSpeed)
                    print("The car is unpaused.")

        elif (str(msg.payload.decode("utf-8")).split(",")[0] == "carLedColor"):
            color = str(msg.payload.decode("utf-8")).split(",")[1]
            print("The car's led color is: " + color)
                    
        elif (str(msg.payload.decode("utf-8")) == "CarGOBottle"):
            carCanGoBottle = True
            if(carCanGoTank == True and carCanGoBottle == True):
                c.publish("car-esp",  "start")
                bw.speed = int(100 * carSpeed)
                c.publish("CarLocation", "onTheWayToContainer")
                lt_status_now = lf.read_digital()
                if lt_status_now == [1,1,1,1,1]:
                    deadLine = True
                carStop = False
                    
        elif (str(msg.payload.decode("utf-8")) == "CarGOTank"):
            carCanGoTank = True
            if(carCanGoTank == True and carCanGoBottle == True):
                c.publish("car-esp",  "start")
                bw.speed = int(100 * carSpeed)
                c.publish("CarLocation", "onTheWayToContainer")
                lt_status_now = lf.read_digital()
                if lt_status_now == [1,1,1,1,1]:
                    deadLine = True
                carStop = False
                    
        elif (str(msg.payload.decode("utf-8")) == "CarGOContainer"):
            c.publish("car-esp",  "start")
            bw.speed = int(100 * carSpeed)
            lt_status_now = lf.read_digital()
            if lt_status_now == [1,1,1,1,1]:
                deadLine = True
            carCanGoContainer = True
            c.publish("CarLocation", "onTheWayToFactory")
            carStop = False
            
        
        
            
            
            
c = mqtt.Client()
c.on_connect = on_connect
c.on_message = on_msg
while (True):
    try:
        c.connect("172.22.50.1", 1883)
        c.loop_start()
        break
    except:
        print("Sikertelen csatlakozas, ujraprobalkozas...")
        time.sleep(5)

picar.setup()

REFERENCES = [400, 400, 330, 400, 400]
#calibrate = True
calibrate = False
forward_speed = 0
backward_speed = 0
turning_angle = 40

max_off_track_count = 40

delay = 0.0005



fw = front_wheels.Front_Wheels(db='/home/pi/SunFounder_PiCar-S/example/config')
bw = back_wheels.Back_Wheels(db='/home/pi/SunFounder_PiCar-S/example/config')
lf = Line_Follower.Line_Follower()

lf.references = REFERENCES
fw.ready()
bw.ready()
fw.turning_max = 45

def heart_beat():
    c.publish("MQTTState", "ONLINE")    

def setup():
    if calibrate:
        cali()

state = States.Factory_to_Container

def main():
    global turning_angle
    off_track_count = 0
    bw.speed = int(forward_speed * carSpeed)

    a_step = 3
    b_step = 17
    c_step = 27
    d_step = 37
    bw.forward()
    global state
    global carCanGoBottle
    global carCanGoTank
    global carCanGoContainer
    global carStop
    global lt_status_now
    global deadLine
    global StopContainer_to_Factory
    global StopFactory_to_Container
    while True:
        #magicnumber = 0
        lt_status_now = lf.read_digital()
        #print(lt_status_now)

        if (deadLine == True and lt_status_now != [1,1,1,1,1]):
            deadLine = False
            
        # Angle calculate
        if	lt_status_now == [0,0,1,0,0]:
            step = 0
        elif lt_status_now == [0,1,1,0,0] or lt_status_now == [0,0,1,1,0]:
            step = a_step
        elif lt_status_now == [0,1,0,0,0] or lt_status_now == [0,0,0,1,0]:
            step = b_step
        elif lt_status_now == [1,1,0,0,0] or lt_status_now == [0,0,0,1,1]:
            step = c_step
        elif lt_status_now == [1,0,0,0,0] or lt_status_now == [0,0,0,0,1]:
            step = d_step
        elif lt_status_now == [1,1,1,1,1] and deadLine == False:                     
            bw.speed = 0
            carStop = True
            if (carCanGoBottle == True and carCanGoTank == True and carCanGoContainer == True):
                c.publish("car-esp",  "stop")
                if(state == States.Container):
                    c.publish("CarLocation", "factory")
                    c.publish("tank-esp", "empty "+color)
                    c.publish("car-esp", "fill "+color)
                    carCanGoTank = False
                    carCanGoBottle = False
                    state = States.Container_to_Factory
                
                elif(state == States.Container_to_Factory):
                    if (StopContainer_to_Factory == True):
                        print("StopContainer_to_Factory true")
                        for i in range(10):
                            heart_beat()
                            time.sleep(0.5)
                    
                    bw.speed = int(100 * carSpeed)
                    bw.forward()
                    time.sleep(0.2)
                    state = States.Factory
                        
                elif(state == States.Factory):
                    c.publish("CarLocation", "container")
                    c.publish("car-esp", "empty "+color)
                    carCanGoContainer = False
                    state = States.Factory_to_Container
                    
                elif(state == States.Factory_to_Container):
                    if (StopFactory_to_Container == True):
                        print("StopFactory_to_Container true")
                        for i in range(10):
                            heart_beat()
                            time.sleep(0.5)
                    bw.speed = int(100 * carSpeed)
                    bw.forward()
                    time.sleep(0.2)
                    state = States.Container
                        
        # Direction calculate
        if	lt_status_now == [0,0,1,0,0]:
            off_track_count = 0
            fw.turn(90)
        # turn right
        elif lt_status_now in ([0,1,1,0,0],[0,1,0,0,0],[1,1,0,0,0],[1,0,0,0,0]):
            off_track_count = 0
            turning_angle = int(90 - step)
        # turn left
        elif lt_status_now in ([0,0,1,1,0],[0,0,0,1,0],[0,0,0,1,1],[0,0,0,0,1]):
            off_track_count = 0
            turning_angle = int(90 + step)
        #elif lt_status_now == [0,0,0,0,0]:
              #off_track_count += 1
              #if off_track_count > max_off_track_count:
                #tmp_angle = -(turning_angle - 90) + 90
                #tmp_angle = (turning_angle-90)/abs(90-turning_angle)
                #tmp_angle *= fw.turning_max
                #bw.speed = int(backward_speed * carSpeed)
                #bw.backward()
                #fw.turn(tmp_angle)
                
                #lf.wait_tile_center()
                #bw.stop()

                #fw.turn(turning_angle)
                #time.sleep(0.2)
                #bw.speed = int(forward_speed * carSpeed)
                #bw.forward()
                #time.sleep(0.2)
                
        else:
            off_track_count = 0
    
        fw.turn(turning_angle)
        time.sleep(delay)
        heart_beat()

def cali():
    references = [0, 0, 0, 0, 0]
    print("cali for module:\n  first put all sensors on white, then put all sensors on black")
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

    for i in range(0, 5):
        references[i] = (white_references[i] + black_references[i]) / 2
    lf.references = references
    print("Middle references =", references)
    time.sleep(1)

def destroy():
    bw.stop()
    fw.turn(90)

if __name__ == '__main__':
    try:
        try:
            while True:
                setup()
                main()
        except Exception as e:
            print(e)
            print('error try again in 5')
            destroy()
            time.sleep(5)
    except KeyboardInterrupt:
        destroy()




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

def on_connect(client, userdata, flag, rc):
    client.subscribe("carSpeed")
    client.subscribe("carTurn")
    print("Connected to mqtt broker!")


def on_msg(client, userdata, msg):
    if (msg.topic == "carSpeed"):        
            bw.speed = int(str(msg.payload.decode("utf-8")))
    if (msg.topic == "carTurn"):
            fw.turn(int(str(msg.payload.decode("utf-8"))))


c = mqtt.Client()
c.username_pw_set("user", "user")
c.on_connect = on_connect
c.on_message = on_msg
while (True):
    try:
        c.connect("10.3.141.3", 1883)
        c.loop_start()
        break
    except:
        print("Sikertelen csatlakozas, ujraprobalkozas...")
        time.sleep(5)

picar.setup()


#calibrate = True
calibrate = False
forward_speed = 0
backward_speed = 0
turning_angle = 0

delay = 0.0005

fw = front_wheels.Front_Wheels(db='/home/pi/SunFounder_PiCar-S/example/config')
bw = back_wheels.Back_Wheels(db='/home/pi/SunFounder_PiCar-S/example/config')

fw.ready()
bw.ready()
fw.turning_max = 45

def setup():
	if calibrate:
		cali()

def main():
	global turning_angle
	off_track_count = 0
	bw.speed = forward_speed

	bw.forward()
	isLoadedddd = False	

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
				print("a")
				#setup()
				#main()
				#straight_run()
		except Exception as e:
			print(e)
			print('error try again in 5')
			destroy()
			time.sleep(5)
	except KeyboardInterrupt:
		destroy()


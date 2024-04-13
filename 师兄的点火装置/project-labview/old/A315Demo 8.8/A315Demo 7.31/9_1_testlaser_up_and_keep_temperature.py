# test socket

import socket
import time

def Openbeam():

    send_data = b"EMON\r"
    server.sendall(send_data)

    # Show status
    send_data = b"STA\r"
    server.sendall(send_data)

    recv_data = server.recv(1024)
    return recv_data


def Closebeam():

    send_data = b"EMOFF\r"
    server.sendall(send_data)
	
    # Show status
    send_data = b"STA\r"
    server.sendall(send_data)

    recv_data = server.recv(1024)
    return recv_data


def Set_power(power):

    send_data = b"SDC " + power + b"\r"
    server.sendall(send_data)

    send_data = b"STA\r"
    server.sendall(send_data)

    recv_data = server.recv(1024)
    return recv_data


def read_present_temperature(filename='bin温度.txt'):
    with open(filename, "r") as f:
        present_temperature = float(f.readlines()[0])
        print(present_temperature)
    return present_temperature


def up_and_keep_temperature(set_temperature, start_power=b"10", offset=5):
    start_time = time.time()

    # Start
    Set_power(power=start_power)
    present_power = start_power
    present_time = start_time
    flag_time = present_time
    present_temperature = read_present_temperature()

    while True:
        # 读取并输出当前温度
        present_time = time.time()
        if present_time - flag_time > 0.5:
            present_temperature = read_present_temperature()

            # 当温度超过设定温度+2度时，减小功率
            if present_temperature > set_temperature + offset:
                present_power = str(float(present_power.decode()) - 0.1).encode()
                Set_power(power=present_power)

            # 当温度小于设定温度-2度时，增加功率升温
            elif (present_temperature < set_temperature - offset) and (present_temperature > set_temperature - 3 * offset):
                present_power = str(float(present_power.decode()) + 0.1).encode()
                Set_power(power=present_power)
            
            # 当温度在设定温度+-2度时，维持该功率
            elif (present_temperature >= set_temperature - offset) and (present_temperature <= set_temperature + offset):
                Set_power(power=present_power)
                time.sleep(60)
                break

            else:
                # 每秒增加1%的功率
                present_power = str(float(present_power.decode()) + 1).encode()
                Set_power(power=present_power)
                flag_time = present_time


                run_time = present_time - start_time
                print("Set_temperature: {:.2f}\tTime: {:.2f}\tTemperature: {:.2f}\tPower: {:s}".format(set_temperature, run_time, present_temperature, present_power.decode()))
        

if __name__=="__main__":

    server = socket.socket(socket.AF_INET,socket.SOCK_STREAM)
    host = "192.168.3.230"
    port = 10001
    server.connect((host, port))

    # Open
    Openbeam()

    # 升温并保持温度
    set_temperature = 200
    up_and_keep_temperature(set_temperature=set_temperature)

    # Close
    Closebeam()

    '''
    flag_time = time.time()
    while True:
        # 读取并输出当前温度
        present_time = time.time()
        if present_time - flag_time > 0.5:
            present_temperature = read_present_temperature()
            flag_time = present_time
    '''

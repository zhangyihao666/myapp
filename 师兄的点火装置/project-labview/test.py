import time

class PIDTemperatureControl:
    def __init__(self, file_path, setpoint, Kp, Ki, Kd):
        self.file_path = file_path
        self.setpoint = setpoint
        self.Kp = Kp
        self.Ki = Ki
        self.Kd = Kd
        self.prev_error = 0
        self.integral = 0

    def read_temperature(self):
        with open(self.file_path, 'r') as file:
            temperature = float(file.readline())
        return temperature

    def pid_control(self, current_temperature):
        error = self.setpoint - current_temperature
        self.integral += error
        derivative = error - self.prev_error
        output = self.Kp * error + self.Ki * self.integral + self.Kd * derivative
        self.prev_error = error
        return output

    def simulate_temperature_control(self):
        for _ in range(100):
            current_temperature = self.read_temperature()
            control_output = self.pid_control(current_temperature)
            print("Current Temperature: {:.2f}C, Control Output: {:.2f}".format(current_temperature, control_output))
            time.sleep(1)

# 使用示例
file_path = "temperature_data.txt"
setpoint = 25.0
Kp = 1.0
Ki = 0
Kd = 0

temperature_controller = PIDTemperatureControl(file_path, setpoint, Kp, Ki, Kd)
temperature_controller.simulate_temperature_control()

import pyautogui
import time
from PySide2.QtUiTools import QUiLoader
from PySide2.QtWidgets import QApplication, QTextBrowser, QMainWindow, QWidget
from PySide2.QtCore import QObject, Signal, Qt, QEvent
import threading
from threading import Thread
import atexit
import background_rc
# 全局事件，用于通知线程终止 安全退出
stop_event = threading.Event()

# 自定义信号源对象类型，一定要继承自 QObject
class MySignals(QObject):
    # 定义一种信号，两个参数 类型分别是： QTextBrowser 和 字符串
    # 调用 emit方法 发信号时，传入参数 必须是这里指定的 参数类型
    text_print = Signal(QTextBrowser,str)

# 实例化
global_ms = MySignals()

class AutoClicker(QMainWindow):
    def __init__(self):
        super().__init__()
        self.ui = QUiLoader().load('clink.ui')
        # 设置部件的样式表
        self.ui.setStyleSheet("#AutoClink{ border-image: url(:/background/zky.svg) }")
        # 自定义信号的处理函数
        global_ms.text_print.connect(self.printToGui)
        global_ms.text_print.emit(self.ui.output, '按下Enter键开始运行程序...')
        # 监听键盘事件
        self.ui.yes.clicked.connect(self.auto_click)
        # 安装事件过滤器
        self.ui.installEventFilter(self)
        self.state = True #可以再次点击
    
    def closeEvent(self, event):
        exit_handler()
        event.accept()

    # 自定义信号的处理函数
    def printToGui(self,fb,text):
        fb.append(str(text))
        fb.ensureCursorVisible()

    # 重写事件过滤器
    def eventFilter(self, obj, event):
        if event.type() == QEvent.KeyPress:
            time.sleep(0.1)
            if event.key() == Qt.Key_Return or event.key() == Qt.Key_Enter:
                self.auto_click()
                return True
        return super().eventFilter(obj, event)

    # 鼠标点击
    def click_mouse(self):
        x, y = pyautogui.position()
        pyautogui.click(x, y)
        global_ms.text_print.emit(self.ui.output,"鼠标点击完成,程序正在运行...")

    # 自动点击
    def auto_click(self):
        def threadFunc():
            if(self.state): 
                self.state = False #不可以再次点击
                #禁用所有控件
                for widget in self.findChildren(QWidget): 
                    widget.setEnabled(False)
                count = self.ui.count.value()
                # 循环点击
                i = 0
                while  i < count :
                    time.sleep(self.ui.time.value())  # 暂停1秒
                    x, y = pyautogui.position()
                    global_ms.text_print.emit(self.ui.output,f"当前鼠标位置：({x}, {y})")
                    # 如果设置了停止事件，则退出循环
                    if stop_event.is_set():
                        break
                    self.click_mouse()   # 执行鼠标点击操作
                    i += 1

                global_ms.text_print.emit(self.ui.output,"程序已结束")
                # 启用所有控件
                for widget in self.findChildren(QWidget):
                    widget.setEnabled(True)
                self.state = True #可以再次点击

        thread = Thread(target=threadFunc)
        thread.daemon = True
        thread.start()
        
# 安全退出
def exit_handler():
    global stop_event
    print("Exiting...")
    # 设置事件以通知线程终止
    stop_event.set()
    # 等待线程结束
    for thread in threading.enumerate():
        if thread != threading.current_thread():
            thread.join()  # 等待线程结束

# 注册退出处理函数
atexit.register(exit_handler)

app = QApplication([])
auto_clicker = AutoClicker()
auto_clicker.ui.show()
app.exec_()

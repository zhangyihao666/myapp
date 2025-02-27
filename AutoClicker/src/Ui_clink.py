# -*- coding: utf-8 -*-

# Form implementation generated from reading ui file 'e:\myapp\AutoClicker\src\clink.ui'
#
# Created by: PyQt5 UI code generator 5.15.7
#
# WARNING: Any manual changes made to this file will be lost when pyuic5 is
# run again.  Do not edit this file unless you know what you are doing.


from PySide2 import QtCore, QtGui, QtWidgets


class Ui_AutoClink(object):
    def setupUi(self, AutoClink):
        AutoClink.setObjectName("AutoClink")
        AutoClink.resize(400, 400)
        AutoClink.setStyleSheet("#AutoClink{\n"
"border-image: url(:/background/zky.svg)\n"
"}")
        self.verticalLayout = QtWidgets.QVBoxLayout(AutoClink)
        self.verticalLayout.setObjectName("verticalLayout")
        self.label_4 = QtWidgets.QLabel(AutoClink)
        self.label_4.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.label_4.setObjectName("label_4")
        self.verticalLayout.addWidget(self.label_4)
        self.output = QtWidgets.QTextBrowser(AutoClink)
        self.output.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.output.setObjectName("output")
        self.verticalLayout.addWidget(self.output)
        self.horizontalLayout = QtWidgets.QHBoxLayout()
        self.horizontalLayout.setObjectName("horizontalLayout")
        self.label = QtWidgets.QLabel(AutoClink)
        self.label.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.label.setObjectName("label")
        self.horizontalLayout.addWidget(self.label)
        self.time = QtWidgets.QSpinBox(AutoClink)
        self.time.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.time.setMaximum(999)
        self.time.setProperty("value", 10)
        self.time.setObjectName("time")
        self.horizontalLayout.addWidget(self.time)
        self.verticalLayout.addLayout(self.horizontalLayout)
        self.horizontalLayout_2 = QtWidgets.QHBoxLayout()
        self.horizontalLayout_2.setObjectName("horizontalLayout_2")
        self.label_2 = QtWidgets.QLabel(AutoClink)
        self.label_2.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.label_2.setObjectName("label_2")
        self.horizontalLayout_2.addWidget(self.label_2)
        self.count = QtWidgets.QSpinBox(AutoClink)
        self.count.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.count.setProperty("value", 10)
        self.count.setObjectName("count")
        self.horizontalLayout_2.addWidget(self.count)
        self.verticalLayout.addLayout(self.horizontalLayout_2)
        self.yes = QtWidgets.QPushButton(AutoClink)
        self.yes.setMouseTracking(False)
        self.yes.setAutoFillBackground(False)
        self.yes.setStyleSheet("background-color: rgba(255, 255, 255, 220);")
        self.yes.setAutoExclusive(True)
        self.yes.setAutoDefault(True)
        self.yes.setObjectName("yes")
        self.verticalLayout.addWidget(self.yes)

        self.retranslateUi(AutoClink)
        QtCore.QMetaObject.connectSlotsByName(AutoClink)

    def retranslateUi(self, AutoClink):
        _translate = QtCore.QCoreApplication.translate
        AutoClink.setWindowTitle(_translate("AutoClink", "AutoClink"))
        self.label_4.setText(_translate("AutoClink", "输出信息"))
        self.output.setHtml(_translate("AutoClink", "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0//EN\" \"http://www.w3.org/TR/REC-html40/strict.dtd\">\n"
"<html><head><meta name=\"qrichtext\" content=\"1\" /><style type=\"text/css\">\n"
"p, li { white-space: pre-wrap; }\n"
"</style></head><body style=\" font-family:\'SimSun\'; font-size:9pt; font-weight:400; font-style:normal;\">\n"
"<p style=\"-qt-paragraph-type:empty; margin-top:0px; margin-bottom:0px; margin-left:0px; margin-right:0px; -qt-block-indent:0; text-indent:0px;\"><br /></p></body></html>"))
        self.label.setText(_translate("AutoClink", "间隔时间（单位：秒）"))
        self.label_2.setText(_translate("AutoClink", "点击次数（单位：次数）"))
        self.yes.setText(_translate("AutoClink", "开始（按enter键）"))
import background_rc

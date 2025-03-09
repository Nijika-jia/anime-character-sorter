from PyQt6.QtWidgets import (QMainWindow, QWidget, QVBoxLayout, QPushButton,
                               QLabel, QFileDialog, QStackedWidget, QProgressBar,
                               QRadioButton, QButtonGroup, QHBoxLayout, QMessageBox,
                               QFrame, QSpacerItem, QSizePolicy, QComboBox, QCheckBox,
                               QSplitter)
from PyQt6.QtCore import Qt, QSize
from PyQt6.QtGui import QPixmap, QImage, QPalette, QColor, QFont, QIcon
from pathlib import Path
from src.core.image_processor import ImageProcessor
from src.core.file_manager import FileManager
from src.ui.recognition_dialog import RecognitionWidget

class StyledButton(QPushButton):
    def __init__(self, text, parent=None):
        super().__init__(text, parent)
        self.setMinimumHeight(35)
        self.setFont(QFont("Microsoft YaHei UI", 10))
        self.setStyleSheet("""
            QPushButton {
                background-color: #4a90e2;
                color: white;
                border: none;
                border-radius: 5px;
                padding: 5px 15px;
            }
            QPushButton:hover {
                background-color: #357abd;
            }
            QPushButton:pressed {
                background-color: #2d6da3;
            }
            QPushButton:disabled {
                background-color: #cccccc;
            }
        """)

class StyledRadioButton(QRadioButton):
    def __init__(self, text, parent=None):
        super().__init__(text, parent)
        self.setFont(QFont("Microsoft YaHei UI", 10))
        self.setStyleSheet("""
            QRadioButton {
                color: #333333;
                spacing: 8px;
            }
            QRadioButton::indicator {
                width: 18px;
                height: 18px;
            }
            QRadioButton::indicator:checked {
                background-color: #4a90e2;
                border: 2px solid #4a90e2;
                border-radius: 9px;
            }
            QRadioButton::indicator:unchecked {
                border: 2px solid #999999;
                border-radius: 9px;
                background-color: white;
            }
        """)

class StyledComboBox(QComboBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFont(QFont("Microsoft YaHei UI", 10))
        self.setMinimumHeight(35)
        self.setStyleSheet("""
            QComboBox {
                background-color: white;
                border: 1px solid #cccccc;
                border-radius: 5px;
                padding: 5px 15px;
                color: #333333;
            }
            QComboBox:hover {
                border: 1px solid #4a90e2;
            }
            QComboBox::drop-down {
                border: none;
                width: 20px;
            }
            QComboBox::down-arrow {
                image: url(down_arrow.png);
                width: 12px;
                height: 12px;
            }
        """)

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("动漫角色图片分类器")
        self.setMinimumSize(1000, 700)
        
        # 设置应用图标
        icon_path = Path(__file__).parent.parent.parent / "resources" / "icon.ico"
        if icon_path.exists():
            self.setWindowIcon(QIcon(str(icon_path)))
        
        # 设置可用的模型
        self.models = {
            "低精度动画模型 (适用于动画原画)": "anime",
            "高精度动画模型① (适用于同人、原画等)": "anime_model_lovelive",
            "高精度动画模型② (适用于各种场景)": "pre_stable",
            "高精度Gal模型①": "full_game_model_kira"
        }
        
        # 保存分类模式状态
        self.is_character_mode = True
        self.is_work_mode = True
        self.is_auto_mode = False
        
        # API错误码映射
        self.error_messages = {
            17701: "图片太大，请确保图片小于4MB",
            17702: "服务器繁忙，请重试",
            17703: "请求参数不正确",
            17704: "API维护中",
            17705: "不支持的图片格式",
            17706: "识别失败（内部错误，请重试）",
            17707: "内部错误",
            17708: "图片中的人物数量超过限制",
            17709: "无法加载统计数据",
            17710: "图片验证码错误",
            17711: "无法完成识别准备工作（请重试）",
            17712: "需要图片名称",
            17720: "识别成功",
            17721: "服务器正常运行中",
            17722: "图片下载失败",
            17723: "未指定Content-Length",
            17724: "不是图片文件或未指定",
            17725: "未指定图片",
            17726: "JSON不接受包含文件",
            17727: "Base64格式错误",
            17728: "已达到本次使用上限",
            17729: "未找到选择的模型",
            17730: "AI图像检测失败",
            17731: "服务器资源过多，请重新尝试",
            17732: "检出已限制",
            17733: "反馈成功",
            17734: "反馈失败",
            17735: "反馈识别效果成功",
            17736: "验证码错误"
        }
        
        # 设置窗口样式
        self.setStyleSheet("""
            QMainWindow {
                background-color: #f5f5f5;
            }
            QLabel {
                color: #333333;
                font-family: "Microsoft YaHei UI";
                font-size: 10pt;
            }
            QProgressBar {
                border: 1px solid #cccccc;
                border-radius: 3px;
                text-align: center;
                background-color: #ffffff;
            }
            QProgressBar::chunk {
                background-color: #4a90e2;
            }
        """)
        
        # 初始化处理器
        self.image_processor = ImageProcessor()
        self.file_manager = FileManager()
        
        # 当前处理的图片列表和索引
        self.current_images = []
        self.current_index = -1
        
        # 分类结果存储
        self.classification_results = {}
        
        # 临时目录
        self.temp_dir = None
        
        self.setup_ui()
    
    def setup_ui(self):
        # 创建并保存主要部件的引用
        self.main_widget = QWidget()
        self.setCentralWidget(self.main_widget)
        
        # 主布局
        layout = QVBoxLayout(self.main_widget)
        layout.setSpacing(20)
        layout.setContentsMargins(20, 20, 20, 20)
        
        # 顶部控制面板
        control_panel = QFrame()
        control_panel.setStyleSheet("""
            QFrame {
                background-color: white;
                border-radius: 10px;
                padding: 15px;
            }
        """)
        control_layout = QVBoxLayout(control_panel)
        
        # 顶部按钮区域
        top_layout = QHBoxLayout()
        top_layout.setSpacing(15)
        
        # 选择文件夹按钮
        self.select_folder_btn = StyledButton("选择图片文件夹")
        self.select_folder_btn.clicked.connect(self.select_folder)
        top_layout.addWidget(self.select_folder_btn)
        
        # 添加第一个分隔线
        separator1 = QFrame()
        separator1.setFrameShape(QFrame.Shape.VLine)
        separator1.setFrameShadow(QFrame.Shadow.Sunken)
        separator1.setStyleSheet("background-color: #cccccc;")
        top_layout.addWidget(separator1)
        
        # 添加模型选择
        model_label = QLabel("识别模型：")
        model_label.setFont(QFont("Microsoft YaHei UI", 10))
        top_layout.addWidget(model_label)
        
        self.model_combo = StyledComboBox()
        for name in self.models.keys():
            self.model_combo.addItem(name)
        top_layout.addWidget(self.model_combo)
        
        # 添加第二个分隔线
        separator2 = QFrame()
        separator2.setFrameShape(QFrame.Shape.VLine)
        separator2.setFrameShadow(QFrame.Shadow.Sunken)
        separator2.setStyleSheet("background-color: #cccccc;")
        top_layout.addWidget(separator2)
        
        # 分类模式选择
        mode_group_label = QLabel("分类模式：")
        mode_group_label.setFont(QFont("Microsoft YaHei UI", 10))
        top_layout.addWidget(mode_group_label)
        
        self.character_mode = QCheckBox("按角色分类")
        self.work_mode = QCheckBox("按作品分类")
        self.auto_mode = QRadioButton("自动分类")
        
        self.character_mode.setFont(QFont("Microsoft YaHei UI", 10))
        self.work_mode.setFont(QFont("Microsoft YaHei UI", 10))
        self.auto_mode.setFont(QFont("Microsoft YaHei UI", 10))
        
        # 设置初始状态
        self.character_mode.setChecked(self.is_character_mode)
        self.work_mode.setChecked(self.is_work_mode)
        self.auto_mode.setChecked(self.is_auto_mode)
        
        # 连接状态变化信号
        self.character_mode.stateChanged.connect(self.update_mode_state)
        self.work_mode.stateChanged.connect(self.update_mode_state)
        self.auto_mode.toggled.connect(self.update_mode_state)
        
        mode_layout = QHBoxLayout()
        mode_layout.setSpacing(15)
        mode_layout.addWidget(self.character_mode)
        mode_layout.addWidget(self.work_mode)
        mode_layout.addWidget(self.auto_mode)
        
        top_layout.addLayout(mode_layout)
        
        # 添加弹性空间
        top_layout.addStretch()
        
        # 开始分类按钮
        self.start_btn = StyledButton("开始分类")
        self.start_btn.clicked.connect(self.start_classification)
        top_layout.addWidget(self.start_btn)
        
        control_layout.addLayout(top_layout)
        layout.addWidget(control_panel)
        
        # 图片显示区域
        image_panel = QFrame()
        image_panel.setStyleSheet("""
            QFrame {
                background-color: white;
                border-radius: 10px;
                padding: 15px;
            }
        """)
        image_layout = QVBoxLayout(image_panel)
        
        self.image_label = QLabel()
        self.image_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.image_label.setMinimumHeight(400)
        self.image_label.setStyleSheet("""
            QLabel {
                background-color: #f8f9fa;
                border-radius: 5px;
            }
        """)
        image_layout.addWidget(self.image_label)
        
        # 底部控制区域
        bottom_layout = QHBoxLayout()
        bottom_layout.setSpacing(15)
        
        # 上一张/下一张按钮
        self.prev_btn = StyledButton("上一张")
        self.next_btn = StyledButton("下一张")
        self.prev_btn.clicked.connect(self.show_previous_image)
        self.next_btn.clicked.connect(self.show_next_image)
        
        bottom_layout.addStretch()
        bottom_layout.addWidget(self.prev_btn)
        bottom_layout.addWidget(self.next_btn)
        bottom_layout.addStretch()
        
        image_layout.addLayout(bottom_layout)
        layout.addWidget(image_panel)
        
        # 进度条面板
        progress_panel = QFrame()
        progress_panel.setStyleSheet("""
            QFrame {
                background-color: white;
                border-radius: 10px;
                padding: 15px;
            }
        """)
        progress_layout = QVBoxLayout(progress_panel)
        
        # 进度条
        self.progress_bar = QProgressBar()
        self.progress_bar.setMinimumHeight(25)
        progress_layout.addWidget(self.progress_bar)
        
        layout.addWidget(progress_panel)
        
        # 添加 API 来源信息
        api_info = QLabel('API provided by <a href="https://www.animetrace.com/">AnimeTrace</a>')
        api_info.setAlignment(Qt.AlignmentFlag.AlignCenter)
        api_info.setOpenExternalLinks(True)  # 允许点击链接
        api_info.setStyleSheet("""
            QLabel {
                color: #666666;
                font-size: 9pt;
            }
            QLabel a {
                color: #4a90e2;
                text-decoration: none;
            }
            QLabel a:hover {
                text-decoration: underline;
            }
        """)
        layout.addWidget(api_info)
        
        # 初始化按钮状态
        self.update_button_states()
    
    def select_folder(self):
        folder = QFileDialog.getExistingDirectory(self, "选择图片文件夹")
        if folder:
            self.current_images = list(Path(folder).glob("*.jpg")) + \
                                list(Path(folder).glob("*.jpeg")) + \
                                list(Path(folder).glob("*.png"))
            if self.current_images:
                self.current_index = 0
                self.show_current_image()
            self.update_button_states()
    
    def show_current_image(self):
        if 0 <= self.current_index < len(self.current_images):
            image_path = self.current_images[self.current_index]
            pixmap = QPixmap(str(image_path))
            scaled_pixmap = pixmap.scaled(QSize(600, 400), 
                                        Qt.AspectRatioMode.KeepAspectRatio,
                                        Qt.TransformationMode.SmoothTransformation)
            self.image_label.setPixmap(scaled_pixmap)
    
    def show_previous_image(self):
        if self.current_index > 0:
            self.current_index -= 1
            self.show_current_image()
            self.update_button_states()
    
    def show_next_image(self):
        if self.current_index < len(self.current_images) - 1:
            self.current_index += 1
            self.show_current_image()
            self.update_button_states()
    
    def update_button_states(self):
        has_images = len(self.current_images) > 0
        self.prev_btn.setEnabled(has_images and self.current_index > 0)
        self.next_btn.setEnabled(has_images and self.current_index < len(self.current_images) - 1)
        self.start_btn.setEnabled(has_images)
    
    def start_classification(self):
        if not self.current_images:
            return
        
        # 保存当前的分类模式状态
        self.update_mode_state()
        # 保存当前选择的模型
        self.current_model = self.models[self.model_combo.currentText()]
        
        if self.auto_mode.isChecked():
            self.auto_classify()
        else:
            self.manual_classify()
    
    def save_results(self, output_dir: Path):
        """保存分类结果"""
        try:
            # 根据保存的模式状态进行分类
            if self.is_character_mode:
                self.file_manager.sort_by_character(self.classification_results, output_dir / "by_character")
            if self.is_work_mode:
                self.file_manager.sort_by_work(self.classification_results, output_dir / "by_work")
            
            # 创建压缩包
            save_path, _ = QFileDialog.getSaveFileName(
                self,
                "选择保存位置",
                str(output_dir.parent / "sorted_images.zip"),
                "ZIP文件 (*.zip)"
            )
            
            if not save_path:  # 用户取消了保存
                return False
                
            save_path = Path(save_path)
            self.file_manager.create_zip(output_dir, save_path)
            
            # 清理临时文件夹
            self.file_manager.cleanup_temp_dir(output_dir)
            
            QMessageBox.information(self, "完成", f"图片分类完成！\n文件已保存至：{save_path}")
            return True
            
        except Exception as e:
            QMessageBox.critical(self, "错误", f"保存文件时出错：{str(e)}")
            return False
    
    def show_error_message(self, code):
        """显示错误信息"""
        if code in self.error_messages:
            QMessageBox.warning(self, "提示", self.error_messages[code])
        else:
            QMessageBox.warning(self, "错误", f"未知错误（错误码：{code}）")
    
    def get_temp_dir(self):
        """获取或创建临时目录"""
        if not self.temp_dir:
            self.temp_dir = self.file_manager.get_temp_dir()
        return self.temp_dir
    
    def closeEvent(self, event):
        """窗口关闭时清理临时文件夹"""
        if self.temp_dir:
            self.file_manager.cleanup_temp_dir(self.temp_dir)
        event.accept()
    
    def update_mode_state(self):
        """更新分类模式状态"""
        self.is_character_mode = self.character_mode.isChecked()
        self.is_work_mode = self.work_mode.isChecked()
        self.is_auto_mode = self.auto_mode.isChecked()
    
    def restore_main_layout(self):
        """恢复主布局"""
        if self.main_widget:
            # 清理当前的中央部件
            current_central = self.centralWidget()
            if current_central:
                current_central.setParent(None)
            
            # 重新创建主布局
            self.setup_ui()
            
            # 恢复复选框状态
            self.character_mode.setChecked(self.is_character_mode)
            self.work_mode.setChecked(self.is_work_mode)
            self.auto_mode.setChecked(self.is_auto_mode)
            
            # 如果有当前图片，显示它
            if self.current_images and 0 <= self.current_index < len(self.current_images):
                self.show_current_image()
                
            # 更新按钮状态
            self.update_button_states()
            
    def manual_classify(self):
        """手动分类模式"""
        if not self.current_images:
            return
        
        # 创建分割器
        splitter = QSplitter(Qt.Orientation.Horizontal)
        
        # 左侧图片显示区域
        left_widget = QWidget()
        left_layout = QVBoxLayout(left_widget)
        
        # 创建新的图片标签
        self.image_label = QLabel()
        self.image_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.image_label.setMinimumHeight(400)
        self.image_label.setStyleSheet("""
            QLabel {
                background-color: #f8f9fa;
                border-radius: 5px;
            }
        """)
        
        # 创建新的进度条
        self.progress_bar = QProgressBar()
        self.progress_bar.setMinimumHeight(25)
        self.progress_bar.setMaximum(len(self.current_images))
        self.progress_bar.setValue(self.current_index + 1)
        
        left_layout.addWidget(self.image_label)
        left_layout.addWidget(self.progress_bar)
        splitter.addWidget(left_widget)
        
        # 使用统一的临时目录
        output_dir = self.get_temp_dir()
        
        # 处理当前图片
        result = self.image_processor.process_image(self.current_images[self.current_index], self.current_model)
        
        # 检查返回结果
        if result.get('code') == 0 and result.get('data'):
            # 获取匹配结果
            char_info = self.image_processor.get_character_info(result)
            if char_info:
                # 创建识别结果组件
                self.recognition_widget = RecognitionWidget(
                    character_info=char_info,
                    show_character=self.is_character_mode,
                    show_work=self.is_work_mode,
                    models=self.models,
                    current_model=self.current_model,
                    image_path=str(self.current_images[self.current_index]),
                    image_processor=self.image_processor
                )
                splitter.addWidget(self.recognition_widget)
                
                # 连接确认按钮信号
                self.recognition_widget.finished.connect(
                    lambda char, work: self.handle_recognition_confirm((char, work))
                )
                self.recognition_widget.cancelled.connect(self.restore_main_layout)
                self.recognition_widget.skipped.connect(self.handle_recognition_skip)
            else:
                # 没有识别结果，创建空的识别结果组件
                self.recognition_widget = RecognitionWidget(
                    character_info=[],
                    show_character=self.is_character_mode,
                    show_work=self.is_work_mode,
                    models=self.models,
                    current_model=self.current_model,
                    image_path=str(self.current_images[self.current_index]),
                    image_processor=self.image_processor
                )
                splitter.addWidget(self.recognition_widget)
                
                # 连接确认按钮信号
                self.recognition_widget.finished.connect(
                    lambda char, work: self.handle_recognition_confirm((char, work))
                )
                self.recognition_widget.cancelled.connect(self.restore_main_layout)
                self.recognition_widget.skipped.connect(self.handle_recognition_skip)
        else:
            # 如果是致命错误，直接返回主界面
            if result.get('code') in [17704, 17728, 17731]:  # API维护中、使用上限、服务器资源不足
                self.show_error_message(result.get('code', -1))
                self.restore_main_layout()
                return
            else:
                # 其他错误，创建空的识别结果组件
                self.recognition_widget = RecognitionWidget(
                    character_info=[],
                    show_character=self.is_character_mode,
                    show_work=self.is_work_mode,
                    models=self.models,
                    current_model=self.current_model,
                    image_path=str(self.current_images[self.current_index]),
                    image_processor=self.image_processor
                )
                splitter.addWidget(self.recognition_widget)
                
                # 连接确认按钮信号
                self.recognition_widget.finished.connect(
                    lambda char, work: self.handle_recognition_confirm((char, work))
                )
                self.recognition_widget.cancelled.connect(self.restore_main_layout)
                self.recognition_widget.skipped.connect(self.handle_recognition_skip)
        
        # 设置中央部件为分割器
        self.setCentralWidget(splitter)
        
        # 显示当前图片
        self.show_current_image()
    
    def handle_recognition_confirm(self, char_info):
        """处理识别结果确认"""
        current_image = self.current_images[self.current_index]
        self.classification_results[current_image] = char_info
        
        # 如果还有下一张图片
        if self.current_index < len(self.current_images) - 1:
            self.current_index += 1
            self.manual_classify()
        else:
            # 所有图片处理完毕，保存结果
            if self.classification_results:
                self.save_results(self.get_temp_dir())
            self.restore_main_layout()
    
    def handle_recognition_skip(self):
        """处理跳过操作"""
        # 如果还有下一张图片
        if self.current_index < len(self.current_images) - 1:
            self.current_index += 1
            self.manual_classify()
        else:
            # 所有图片处理完毕，保存结果
            if self.classification_results:
                self.save_results(self.get_temp_dir())
            self.restore_main_layout()
    
    def auto_classify(self):
        """自动分类模式"""
        if not self.current_images:
            return
            
        self.progress_bar.setMaximum(len(self.current_images))
        self.progress_bar.setValue(0)
        
        # 使用统一的临时目录
        output_dir = self.get_temp_dir()
        
        for i, image_path in enumerate(self.current_images):
            # 处理图片，使用保存的模型
            result = self.image_processor.process_image(image_path, self.current_model)
            
            # 检查返回结果
            if result.get('code') == 0 and result.get('data'):
                # 获取第一个匹配结果
                char_info = self.image_processor.get_character_info(result)
                if char_info:
                    character, work = char_info[0]
                    self.classification_results[image_path] = (character, work)
            else:
                # 如果是致命错误，显示错误信息并返回
                if result.get('code') in [17704, 17728, 17731]:  # API维护中、使用上限、服务器资源不足
                    self.show_error_message(result.get('code', -1))
                    return
            
            self.progress_bar.setValue(i + 1)
        
        # 保存结果
        if self.classification_results:
            self.save_results(output_dir) 
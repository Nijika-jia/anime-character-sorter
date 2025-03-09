from PyQt6.QtWidgets import (QWidget, QVBoxLayout, QHBoxLayout, QPushButton,
                               QTreeWidget, QTreeWidgetItem, QLabel, QLineEdit,
                               QFrame, QMessageBox, QComboBox, QCompleter)
from PyQt6.QtCore import Qt, pyqtSignal
from PyQt6.QtGui import QFont
from src.core.history_manager import HistoryManager

class RecognitionWidget(QWidget):
    # 添加信号用于通知主窗口选择结果
    finished = pyqtSignal(str, str)  # 发送角色名和作品名
    cancelled = pyqtSignal()
    skipped = pyqtSignal()  # 新增跳过信号
    
    def __init__(self, parent=None, character_info=None, show_character=True, show_work=True, models=None, current_model=None, image_path=None, image_processor=None):
        super().__init__(parent)
        self.character_info = character_info or []
        self.show_character = show_character
        self.show_work = show_work
        self.models = models or {}
        self.current_model = current_model
        self.image_path = image_path
        self.image_processor = image_processor
        self.selected_item = None
        
        # 初始化历史记录管理器
        self.history_manager = HistoryManager()
        
        self.setup_ui()
        
    def setup_ui(self):
        self.setMinimumWidth(300)  # 减小最小宽度以适应主窗口
        
        layout = QVBoxLayout(self)
        layout.setSpacing(10)  # 减小间距使布局更紧凑
        
        # 添加标题
        title_label = QLabel("识别结果")
        title_label.setFont(QFont("Microsoft YaHei UI", 12, QFont.Weight.Bold))
        title_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(title_label)
        
        # 添加模型选择区域
        model_frame = QFrame()
        model_frame.setStyleSheet("""
            QFrame {
                background-color: #f8f9fa;
                border-radius: 5px;
                padding: 5px;
            }
        """)
        model_layout = QHBoxLayout(model_frame)
        
        model_label = QLabel("识别模型：")
        model_label.setFont(QFont("Microsoft YaHei UI", 10))
        self.model_combo = QComboBox()
        self.model_combo.setFont(QFont("Microsoft YaHei UI", 10))
        self.model_combo.setMinimumHeight(30)
        
        # 添加模型选项
        for name in self.models.keys():
            self.model_combo.addItem(name)
        # 设置当前选中的模型
        if self.current_model:
            for i, (name, model) in enumerate(self.models.items()):
                if model == self.current_model:
                    self.model_combo.setCurrentIndex(i)
                    break
        
        retry_btn = QPushButton("重新识别")
        retry_btn.setFont(QFont("Microsoft YaHei UI", 10))
        retry_btn.setMinimumHeight(30)
        retry_btn.clicked.connect(self.retry_recognition)
        
        model_layout.addWidget(model_label)
        model_layout.addWidget(self.model_combo)
        model_layout.addWidget(retry_btn)
        
        layout.addWidget(model_frame)
        
        # 角色选择区域
        if self.show_character:
            # 角色提示标签
            char_label = QLabel("请选择正确的角色：")
            char_label.setFont(QFont("Microsoft YaHei UI", 10))
            layout.addWidget(char_label)
            
            # 角色自定义输入
            char_custom_frame = QFrame()
            char_custom_frame.setStyleSheet("""
                QFrame {
                    background-color: #f8f9fa;
                    border-radius: 5px;
                    padding: 10px;
                }
            """)
            char_custom_layout = QHBoxLayout(char_custom_frame)
            
            char_input_label = QLabel("自定义角色：")
            char_input_label.setFont(QFont("Microsoft YaHei UI", 10))
            self.char_input = QLineEdit()
            self.char_input.setPlaceholderText("输入角色名称")
            self.char_input.setFont(QFont("Microsoft YaHei UI", 10))
            
            # 添加自动完成功能
            char_completer = QCompleter(self.history_manager.get_character_suggestions())
            char_completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
            self.char_input.setCompleter(char_completer)
            
            char_custom_layout.addWidget(char_input_label)
            char_custom_layout.addWidget(self.char_input)
            layout.addWidget(char_custom_frame)
            
            # 角色树形视图
            self.char_tree = QTreeWidget()
            self.char_tree.setHeaderLabels(["角色"])
            self.char_tree.setColumnCount(1)
            self.char_tree.setFont(QFont("Microsoft YaHei UI", 10))
            
            # 添加历史记录分组
            if len(self.history_manager.get_character_suggestions()) > 1:  # 如果有历史记录（不只是unknown）
                history_group = QTreeWidgetItem(["历史记录"])
                self.char_tree.addTopLevelItem(history_group)
                for char in self.history_manager.get_character_suggestions():
                    if char != "unknown":  # 不在历史记录中显示unknown
                        item = QTreeWidgetItem([char])
                        history_group.addChild(item)
                history_group.setExpanded(True)
            
            # 添加Unknown选项
            unknown_char = QTreeWidgetItem(["unknown"])
            self.char_tree.addTopLevelItem(unknown_char)
            
            # 添加分隔线
            separator_char = QTreeWidgetItem(["-" * 30])
            separator_char.setFlags(separator_char.flags() & ~Qt.ItemFlag.ItemIsSelectable)
            self.char_tree.addTopLevelItem(separator_char)
            
            # 添加识别结果分组
            if self.character_info:
                results_group = QTreeWidgetItem(["识别结果"])
                self.char_tree.addTopLevelItem(results_group)
                # 添加角色列表
                added_chars = set()
                first_char_item = None
                for character, _ in self.character_info:
                    if character not in added_chars:
                        item = QTreeWidgetItem([character])
                        results_group.addChild(item)
                        added_chars.add(character)
                        if first_char_item is None:
                            first_char_item = item
                results_group.setExpanded(True)
            
            self.char_tree.resizeColumnToContents(0)
            layout.addWidget(self.char_tree)
            
            # 自动选择第一个角色
            if first_char_item:
                self.char_tree.setCurrentItem(first_char_item)
                self.char_input.setText(first_char_item.text(0))
        
        # 作品选择区域
        if self.show_work:
            # 作品提示标签
            work_label = QLabel("请选择正确的作品：")
            work_label.setFont(QFont("Microsoft YaHei UI", 10))
            layout.addWidget(work_label)
            
            # 作品自定义输入
            work_custom_frame = QFrame()
            work_custom_frame.setStyleSheet("""
                QFrame {
                    background-color: #f8f9fa;
                    border-radius: 5px;
                    padding: 10px;
                }
            """)
            work_custom_layout = QHBoxLayout(work_custom_frame)
            
            work_input_label = QLabel("自定义作品：")
            work_input_label.setFont(QFont("Microsoft YaHei UI", 10))
            self.work_input = QLineEdit()
            self.work_input.setPlaceholderText("输入作品名称")
            self.work_input.setFont(QFont("Microsoft YaHei UI", 10))
            
            # 添加自动完成功能
            work_completer = QCompleter(self.history_manager.get_work_suggestions())
            work_completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
            self.work_input.setCompleter(work_completer)
            
            work_custom_layout.addWidget(work_input_label)
            work_custom_layout.addWidget(self.work_input)
            layout.addWidget(work_custom_frame)
            
            # 作品树形视图
            self.work_tree = QTreeWidget()
            self.work_tree.setHeaderLabels(["作品"])
            self.work_tree.setColumnCount(1)
            self.work_tree.setFont(QFont("Microsoft YaHei UI", 10))
            
            # 添加历史记录分组
            if len(self.history_manager.get_work_suggestions()) > 1:  # 如果有历史记录（不只是unknown）
                history_group = QTreeWidgetItem(["历史记录"])
                self.work_tree.addTopLevelItem(history_group)
                for work in self.history_manager.get_work_suggestions():
                    if work != "unknown":  # 不在历史记录中显示unknown
                        item = QTreeWidgetItem([work])
                        history_group.addChild(item)
                history_group.setExpanded(True)
            
            # 添加Unknown选项
            unknown_work = QTreeWidgetItem(["unknown"])
            self.work_tree.addTopLevelItem(unknown_work)
            
            # 添加分隔线
            separator_work = QTreeWidgetItem(["-" * 30])
            separator_work.setFlags(separator_work.flags() & ~Qt.ItemFlag.ItemIsSelectable)
            self.work_tree.addTopLevelItem(separator_work)
            
            # 添加识别结果分组
            if self.character_info:
                results_group = QTreeWidgetItem(["识别结果"])
                self.work_tree.addTopLevelItem(results_group)
                # 添加作品列表
                added_works = set()
                first_work_item = None
                for _, work in self.character_info:
                    if work not in added_works:
                        item = QTreeWidgetItem([work])
                        results_group.addChild(item)
                        added_works.add(work)
                        if first_work_item is None:
                            first_work_item = item
                results_group.setExpanded(True)
            
            self.work_tree.resizeColumnToContents(0)
            layout.addWidget(self.work_tree)
            
            # 自动选择第一个作品
            if first_work_item:
                self.work_tree.setCurrentItem(first_work_item)
                self.work_input.setText(first_work_item.text(0))
        
        # 修改按钮区域
        button_layout = QHBoxLayout()
        
        self.skip_btn = QPushButton("跳过")
        self.skip_btn.setFont(QFont("Microsoft YaHei UI", 10))
        self.skip_btn.setStyleSheet("""
            QPushButton {
                background-color: #ffc107;
                color: black;
                border: none;
                border-radius: 5px;
                padding: 5px 15px;
            }
            QPushButton:hover {
                background-color: #ffb300;
            }
            QPushButton:pressed {
                background-color: #ffa000;
            }
        """)
        self.skip_btn.clicked.connect(self.on_skip)
        
        self.confirm_btn = QPushButton("确认")
        self.confirm_btn.setFont(QFont("Microsoft YaHei UI", 10))
        self.confirm_btn.clicked.connect(self.on_confirm)
        
        self.cancel_btn = QPushButton("取消")
        self.cancel_btn.setFont(QFont("Microsoft YaHei UI", 10))
        self.cancel_btn.clicked.connect(self.on_cancel)
        
        button_layout.addStretch()
        button_layout.addWidget(self.skip_btn)
        button_layout.addWidget(self.confirm_btn)
        button_layout.addWidget(self.cancel_btn)
        
        layout.addLayout(button_layout)
        
        # 添加弹性空间
        layout.addStretch()
        
        # 绑定事件
        if self.show_character:
            self.char_tree.itemSelectionChanged.connect(self.on_char_selection_changed)
            self.char_input.textChanged.connect(self.update_button_state)
        if self.show_work:
            self.work_tree.itemSelectionChanged.connect(self.on_work_selection_changed)
            self.work_input.textChanged.connect(self.update_button_state)
        
        self.update_button_state()
    
    def on_char_selection_changed(self):
        """当角色选择改变时更新输入框"""
        selected_items = self.char_tree.selectedItems()
        if selected_items:
            item = selected_items[0]
            if item.text(0) != "-" * 30:  # 不是分隔线
                self.char_input.setText(item.text(0))
    
    def on_work_selection_changed(self):
        """当作品选择改变时更新输入框"""
        selected_items = self.work_tree.selectedItems()
        if selected_items:
            item = selected_items[0]
            if item.text(0) != "-" * 30:  # 不是分隔线
                self.work_input.setText(item.text(0))
    
    def update_button_state(self):
        """更新确认按钮状态"""
        enabled = True
        
        if self.show_character:
            has_char = bool(self.char_input.text().strip()) or len(self.char_tree.selectedItems()) > 0
            enabled = enabled and has_char
        
        if self.show_work:
            has_work = bool(self.work_input.text().strip()) or len(self.work_tree.selectedItems()) > 0
            enabled = enabled and has_work
        
        self.confirm_btn.setEnabled(enabled)
    
    def get_selected_info(self):
        """获取选中的信息"""
        character = "unknown"
        work = "unknown"
        
        if self.show_character:
            # 优先使用自定义输入
            char_name = self.char_input.text().strip()
            if char_name:
                character = char_name
            else:
                # 使用选中项
                selected_items = self.char_tree.selectedItems()
                if selected_items:
                    item = selected_items[0]
                    if item.text(0) != "-" * 30:  # 不是分隔线
                        character = item.text(0)
        
        if self.show_work:
            # 优先使用自定义输入
            work_name = self.work_input.text().strip()
            if work_name:
                work = work_name
            else:
                # 使用选中项
                selected_items = self.work_tree.selectedItems()
                if selected_items:
                    item = selected_items[0]
                    if item.text(0) != "-" * 30:  # 不是分隔线
                        work = item.text(0)
        
        return character, work

    def retry_recognition(self):
        """使用新选择的模型重新识别图片"""
        if not self.image_path or not self.image_processor:
            return
            
        # 获取选中的模型
        selected_model_name = self.model_combo.currentText()
        selected_model = self.models[selected_model_name]
        
        # 重新处理图片
        result = self.image_processor.process_image(self.image_path, selected_model)
        if result.get('code') == 0 and result.get('data'):
            # 获取新的识别结果
            char_info = self.image_processor.get_character_info(result)
            if char_info:
                self.character_info = char_info
                # 更新界面
                self.update_recognition_results()
                return
        
        # 如果识别失败，显示提示
        QMessageBox.warning(self, "识别失败", "使用新模型识别失败，请尝试其他模型或手动输入。")
    
    def update_recognition_results(self):
        """更新识别结果显示"""
        if self.show_character:
            # 清空原有列表
            self.char_tree.clear()
            
            # 添加历史记录分组
            if len(self.history_manager.get_character_suggestions()) > 1:
                history_group = QTreeWidgetItem(["历史记录"])
                self.char_tree.addTopLevelItem(history_group)
                for char in self.history_manager.get_character_suggestions():
                    if char != "unknown":
                        item = QTreeWidgetItem([char])
                        history_group.addChild(item)
                history_group.setExpanded(True)
            
            # 添加Unknown选项
            unknown_char = QTreeWidgetItem(["unknown"])
            self.char_tree.addTopLevelItem(unknown_char)
            
            # 添加分隔线
            separator_char = QTreeWidgetItem(["-" * 30])
            separator_char.setFlags(separator_char.flags() & ~Qt.ItemFlag.ItemIsSelectable)
            self.char_tree.addTopLevelItem(separator_char)
            
            # 添加识别结果分组
            if self.character_info:
                results_group = QTreeWidgetItem(["识别结果"])
                self.char_tree.addTopLevelItem(results_group)
                # 添加新的角色列表
                added_chars = set()
                for character, _ in self.character_info:
                    if character not in added_chars:
                        item = QTreeWidgetItem([character])
                        results_group.addChild(item)
                        added_chars.add(character)
                results_group.setExpanded(True)
            
            self.char_tree.resizeColumnToContents(0)
        
        if self.show_work:
            # 清空原有列表
            self.work_tree.clear()
            
            # 添加历史记录分组
            if len(self.history_manager.get_work_suggestions()) > 1:
                history_group = QTreeWidgetItem(["历史记录"])
                self.work_tree.addTopLevelItem(history_group)
                for work in self.history_manager.get_work_suggestions():
                    if work != "unknown":
                        item = QTreeWidgetItem([work])
                        history_group.addChild(item)
                history_group.setExpanded(True)
            
            # 添加Unknown选项
            unknown_work = QTreeWidgetItem(["unknown"])
            self.work_tree.addTopLevelItem(unknown_work)
            
            # 添加分隔线
            separator_work = QTreeWidgetItem(["-" * 30])
            separator_work.setFlags(separator_work.flags() & ~Qt.ItemFlag.ItemIsSelectable)
            self.work_tree.addTopLevelItem(separator_work)
            
            # 添加识别结果分组
            if self.character_info:
                results_group = QTreeWidgetItem(["识别结果"])
                self.work_tree.addTopLevelItem(results_group)
                # 添加新的作品列表
                added_works = set()
                for _, work in self.character_info:
                    if work not in added_works:
                        item = QTreeWidgetItem([work])
                        results_group.addChild(item)
                        added_works.add(work)
                results_group.setExpanded(True)
            
            self.work_tree.resizeColumnToContents(0)

    def on_confirm(self):
        """确认选择"""
        character, work = self.get_selected_info()
        
        # 保存到历史记录
        if character != "unknown":
            self.history_manager.add_character(character)
        if work != "unknown":
            self.history_manager.add_work(work)
        
        self.finished.emit(character, work)
        
    def on_cancel(self):
        """取消选择"""
        self.cancelled.emit()
    
    def on_skip(self):
        """跳过当前图片"""
        self.skipped.emit() 
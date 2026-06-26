namespace OPods.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Label deviceLabel;
    private Label deviceNameLabel;
    private Label statusDotLabel;
    private Label statusTextLabel;
    private Button changeDeviceButton;
    private Button refreshButton;

    private GroupBox batteryGroup;
    private Label leftBatteryLabel;
    private Label rightBatteryLabel;
    private Label caseBatteryLabel;

    private GroupBox deviceInfoGroup;
    private Label codecLabel;
    private Label codecValueLabel;
    private Label wearLeftLabel;
    private Label wearRightLabel;

    private GroupBox ancGroup;
    private FlowLayoutPanel ancButtonPanel;

    private GroupBox gameModeGroup;
    private CheckBox gameModeCheckBox;
    private Label gameModeImplLabel;
    private ComboBox gameModeImplCombo;

    private GroupBox spatialAudioGroup;
    private CheckBox spatialAudioCheckBox;

    private GroupBox eqGroup;
    private Label eqPresetLabel;
    private ComboBox eqPresetCombo;
    private Label eqRawIdLabel;
    private NumericUpDown eqRawIdInput;

    private GroupBox logGroup;
    private TextBox logTextBox;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        deviceLabel = new Label();
        deviceNameLabel = new Label();
        statusDotLabel = new Label();
        statusTextLabel = new Label();
        changeDeviceButton = new Button();
        refreshButton = new Button();

        batteryGroup = new GroupBox();
        leftBatteryLabel = new Label();
        rightBatteryLabel = new Label();
        caseBatteryLabel = new Label();

        deviceInfoGroup = new GroupBox();
        codecLabel = new Label();
        codecValueLabel = new Label();
        wearLeftLabel = new Label();
        wearRightLabel = new Label();

        ancGroup = new GroupBox();
        ancButtonPanel = new FlowLayoutPanel();

        gameModeGroup = new GroupBox();
        gameModeCheckBox = new CheckBox();
        gameModeImplLabel = new Label();
        gameModeImplCombo = new ComboBox();

        spatialAudioGroup = new GroupBox();
        spatialAudioCheckBox = new CheckBox();

        eqGroup = new GroupBox();
        eqPresetLabel = new Label();
        eqPresetCombo = new ComboBox();
        eqRawIdLabel = new Label();
        eqRawIdInput = new NumericUpDown();

        logGroup = new GroupBox();
        logTextBox = new TextBox();

        SuspendLayout();

        // deviceLabel
        deviceLabel.AutoSize = true;
        deviceLabel.Location = new Point(16, 18);
        deviceLabel.Text = "当前设备：";

        // deviceNameLabel
        deviceNameLabel.AutoSize = true;
        deviceNameLabel.Location = new Point(85, 18);
        deviceNameLabel.Font = new Font(Font, FontStyle.Bold);
        deviceNameLabel.Text = "(未连接)";

        // statusDotLabel
        statusDotLabel.AutoSize = true;
        statusDotLabel.Location = new Point(16, 44);
        statusDotLabel.Text = "●";
        statusDotLabel.ForeColor = SystemColors.ControlDark;

        // statusTextLabel
        statusTextLabel.AutoSize = true;
        statusTextLabel.Location = new Point(36, 44);
        statusTextLabel.Text = "未连接";

        // changeDeviceButton
        changeDeviceButton.Location = new Point(16, 70);
        changeDeviceButton.Size = new Size(120, 32);
        changeDeviceButton.Text = "更换设备";
        changeDeviceButton.UseVisualStyleBackColor = true;
        changeDeviceButton.Click += ChangeDeviceButton_Click;

        // refreshButton
        refreshButton.Location = new Point(146, 70);
        refreshButton.Size = new Size(120, 32);
        refreshButton.Text = "刷新状态";
        refreshButton.UseVisualStyleBackColor = true;
        refreshButton.Click += RefreshButton_Click;

        // batteryGroup
        batteryGroup.Location = new Point(16, 112);
        batteryGroup.Size = new Size(560, 110);
        batteryGroup.Text = "电量";

        // leftBatteryLabel
        leftBatteryLabel.Location = new Point(20, 28);
        leftBatteryLabel.Size = new Size(160, 64);
        leftBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        leftBatteryLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        leftBatteryLabel.Text = "左耳\n--";

        // rightBatteryLabel
        rightBatteryLabel.Location = new Point(200, 28);
        rightBatteryLabel.Size = new Size(160, 64);
        rightBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        rightBatteryLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        rightBatteryLabel.Text = "右耳\n--";

        // caseBatteryLabel
        caseBatteryLabel.Location = new Point(380, 28);
        caseBatteryLabel.Size = new Size(160, 64);
        caseBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        caseBatteryLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        caseBatteryLabel.Text = "充电盒\n--";

        batteryGroup.Controls.Add(leftBatteryLabel);
        batteryGroup.Controls.Add(rightBatteryLabel);
        batteryGroup.Controls.Add(caseBatteryLabel);

        // deviceInfoGroup —— 编解码器 + 佩戴状态
        deviceInfoGroup.Location = new Point(16, 232);
        deviceInfoGroup.Size = new Size(560, 90);
        deviceInfoGroup.Text = "设备信息";

        // codecLabel
        codecLabel.AutoSize = true;
        codecLabel.Location = new Point(20, 30);
        codecLabel.Text = "编解码器：";

        // codecValueLabel
        codecValueLabel.AutoSize = true;
        codecValueLabel.Location = new Point(90, 30);
        codecValueLabel.Font = new Font(Font, FontStyle.Bold);
        codecValueLabel.Text = "--";

        // wearLeftLabel
        wearLeftLabel.Location = new Point(20, 56);
        wearLeftLabel.Size = new Size(260, 24);
        wearLeftLabel.TextAlign = ContentAlignment.MiddleLeft;
        wearLeftLabel.Text = "左耳：--";

        // wearRightLabel
        wearRightLabel.Location = new Point(290, 56);
        wearRightLabel.Size = new Size(260, 24);
        wearRightLabel.TextAlign = ContentAlignment.MiddleLeft;
        wearRightLabel.Text = "右耳：--";

        deviceInfoGroup.Controls.Add(codecLabel);
        deviceInfoGroup.Controls.Add(codecValueLabel);
        deviceInfoGroup.Controls.Add(wearLeftLabel);
        deviceInfoGroup.Controls.Add(wearRightLabel);

        // ancGroup
        ancGroup.Location = new Point(16, 332);
        ancGroup.Size = new Size(560, 130);
        ancGroup.Text = "降噪模式";

        // ancButtonPanel —— 动态 ANC 按钮容器（代码按 profile 生成）
        ancButtonPanel.Dock = DockStyle.Fill;
        ancButtonPanel.FlowDirection = FlowDirection.TopDown;
        ancButtonPanel.WrapContents = false;
        ancButtonPanel.AutoScroll = true;
        ancButtonPanel.Padding = new Padding(8, 26, 8, 4);

        ancGroup.Controls.Add(ancButtonPanel);

        // gameModeGroup
        gameModeGroup.Location = new Point(16, 472);
        gameModeGroup.Size = new Size(560, 80);
        gameModeGroup.Text = "游戏模式";

        // gameModeCheckBox
        gameModeCheckBox.Location = new Point(20, 34);
        gameModeCheckBox.Size = new Size(120, 28);
        gameModeCheckBox.Text = "开启";
        gameModeCheckBox.CheckedChanged += GameModeCheckBox_CheckedChanged;

        // gameModeImplLabel
        gameModeImplLabel.AutoSize = true;
        gameModeImplLabel.Location = new Point(160, 38);
        gameModeImplLabel.Text = "实现方式：";

        // gameModeImplCombo
        gameModeImplCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        gameModeImplCombo.Location = new Point(245, 34);
        gameModeImplCombo.Size = new Size(160, 28);
        gameModeImplCombo.Items.AddRange(new object[] { "STANDARD", "COMPATIBLE" });
        gameModeImplCombo.SelectedIndexChanged += GameModeImplCombo_SelectedIndexChanged;

        gameModeGroup.Controls.Add(gameModeCheckBox);
        gameModeGroup.Controls.Add(gameModeImplLabel);
        gameModeGroup.Controls.Add(gameModeImplCombo);

        // spatialAudioGroup —— 仅当 Capabilities.SupportsSpatialAudio 时显示
        spatialAudioGroup.Location = new Point(16, 562);
        spatialAudioGroup.Size = new Size(560, 64);
        spatialAudioGroup.Text = "空间音频";
        spatialAudioGroup.Visible = false;

        // spatialAudioCheckBox
        spatialAudioCheckBox.Location = new Point(20, 28);
        spatialAudioCheckBox.Size = new Size(160, 28);
        spatialAudioCheckBox.Text = "开启空间音频";
        spatialAudioCheckBox.CheckedChanged += SpatialAudioCheckBox_CheckedChanged;

        spatialAudioGroup.Controls.Add(spatialAudioCheckBox);

        // eqGroup
        eqGroup.Location = new Point(16, 636);
        eqGroup.Size = new Size(560, 80);
        eqGroup.Text = "大师调音 (EQ)";

        // eqPresetLabel
        eqPresetLabel.AutoSize = true;
        eqPresetLabel.Location = new Point(20, 38);
        eqPresetLabel.Text = "预设：";

        // eqPresetCombo —— 内容由代码按 profile 动态填充
        eqPresetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        eqPresetCombo.Location = new Point(85, 34);
        eqPresetCombo.Size = new Size(180, 28);
        eqPresetCombo.SelectedIndexChanged += EqPresetCombo_SelectedIndexChanged;

        // eqRawIdLabel / eqRawIdInput —— 无预设名时回退到原始 preset id 输入
        eqRawIdLabel.AutoSize = true;
        eqRawIdLabel.Location = new Point(20, 38);
        eqRawIdLabel.Text = "Preset ID：";
        eqRawIdLabel.Visible = false;

        eqRawIdInput.Location = new Point(95, 34);
        eqRawIdInput.Size = new Size(80, 28);
        eqRawIdInput.Minimum = 0;
        eqRawIdInput.Maximum = 255;
        eqRawIdInput.Visible = false;
        eqRawIdInput.ValueChanged += EqRawIdInput_ValueChanged;

        eqGroup.Controls.Add(eqPresetLabel);
        eqGroup.Controls.Add(eqPresetCombo);
        eqGroup.Controls.Add(eqRawIdLabel);
        eqGroup.Controls.Add(eqRawIdInput);

        // logGroup
        logGroup.Location = new Point(16, 726);
        logGroup.Size = new Size(560, 130);
        logGroup.Text = "日志";

        // logTextBox
        logTextBox.Location = new Point(12, 22);
        logTextBox.Size = new Size(536, 100);
        logTextBox.Multiline = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.ReadOnly = true;
        logTextBox.Font = new Font("Consolas", 9F);

        logGroup.Controls.Add(logTextBox);

        // MainForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(592, 866);
        Font = new Font("Segoe UI", 9F);
        Controls.Add(deviceLabel);
        Controls.Add(deviceNameLabel);
        Controls.Add(statusDotLabel);
        Controls.Add(statusTextLabel);
        Controls.Add(changeDeviceButton);
        Controls.Add(refreshButton);
        Controls.Add(batteryGroup);
        Controls.Add(deviceInfoGroup);
        Controls.Add(ancGroup);
        Controls.Add(gameModeGroup);
        Controls.Add(spatialAudioGroup);
        Controls.Add(eqGroup);
        Controls.Add(logGroup);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "OPods — OPPO 耳机控制";
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
        PerformLayout();
    }
}

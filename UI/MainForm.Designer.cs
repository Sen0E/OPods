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

    private GroupBox ancGroup;
    private FlowLayoutPanel ancButtonPanel;

    private GroupBox gameModeGroup;
    private CheckBox gameModeCheckBox;
    private Label gameModeImplLabel;
    private ComboBox gameModeImplCombo;

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

        ancGroup = new GroupBox();
        ancButtonPanel = new FlowLayoutPanel();

        gameModeGroup = new GroupBox();
        gameModeCheckBox = new CheckBox();
        gameModeImplLabel = new Label();
        gameModeImplCombo = new ComboBox();

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
        statusDotLabel.ForeColor = Color.Gray;

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
        leftBatteryLabel.Location = new Point(20, 30);
        leftBatteryLabel.Size = new Size(160, 60);
        leftBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        leftBatteryLabel.Text = "左耳\n--";

        // rightBatteryLabel
        rightBatteryLabel.Location = new Point(200, 30);
        rightBatteryLabel.Size = new Size(160, 60);
        rightBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        rightBatteryLabel.Text = "右耳\n--";

        // caseBatteryLabel
        caseBatteryLabel.Location = new Point(380, 30);
        caseBatteryLabel.Size = new Size(160, 60);
        caseBatteryLabel.TextAlign = ContentAlignment.MiddleCenter;
        caseBatteryLabel.Text = "充电盒\n--";

        batteryGroup.Controls.Add(leftBatteryLabel);
        batteryGroup.Controls.Add(rightBatteryLabel);
        batteryGroup.Controls.Add(caseBatteryLabel);

        // ancGroup
        ancGroup.Location = new Point(16, 232);
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
        gameModeGroup.Location = new Point(16, 372);
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

        // logGroup
        logGroup.Location = new Point(16, 462);
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
        ClientSize = new Size(592, 602);
        Controls.Add(deviceLabel);
        Controls.Add(deviceNameLabel);
        Controls.Add(statusDotLabel);
        Controls.Add(statusTextLabel);
        Controls.Add(changeDeviceButton);
        Controls.Add(refreshButton);
        Controls.Add(batteryGroup);
        Controls.Add(ancGroup);
        Controls.Add(gameModeGroup);
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

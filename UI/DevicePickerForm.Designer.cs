namespace OPods.UI;

partial class DevicePickerForm
{
    private System.ComponentModel.IContainer components = null;

    private Button scanButton;
    private Button connectButton;
    private Button cancelButton;
    private ListView deviceList;
    private Label connectionMethodLabel;
    private ComboBox connectionMethodCombo;
    private Label statusLabel;
    private ProgressBar scanProgress;

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

        scanButton = new Button();
        connectButton = new Button();
        cancelButton = new Button();
        deviceList = new ListView();
        connectionMethodLabel = new Label();
        connectionMethodCombo = new ComboBox();
        statusLabel = new Label();
        scanProgress = new ProgressBar();

        SuspendLayout();

        // scanButton
        scanButton.Location = new Point(12, 12);
        scanButton.Size = new Size(100, 32);
        scanButton.Text = "扫描设备";
        scanButton.UseVisualStyleBackColor = true;
        scanButton.Click += ScanButton_Click;

        // statusLabel
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(118, 21);
        statusLabel.Text = "点击「扫描设备」开始";

        // scanProgress
        scanProgress.Location = new Point(12, 48);
        scanProgress.Size = new Size(560, 6);
        scanProgress.Style = ProgressBarStyle.Marquee;
        scanProgress.MarqueeAnimationSpeed = 30;
        scanProgress.Visible = false;

        // deviceList
        deviceList.Location = new Point(12, 60);
        deviceList.Size = new Size(560, 280);
        deviceList.View = View.Details;
        deviceList.FullRowSelect = true;
        deviceList.MultiSelect = false;
        deviceList.GridLines = true;
        deviceList.Columns.Add("设备名称", 220);
        deviceList.Columns.Add("地址", 180);
        deviceList.Columns.Add("已配对", 60);
        deviceList.Columns.Add("已连接", 60);
        deviceList.SelectedIndexChanged += DeviceList_SelectedIndexChanged;

        // connectionMethodLabel
        connectionMethodLabel.AutoSize = true;
        connectionMethodLabel.Location = new Point(12, 350);
        connectionMethodLabel.Text = "连接方式：";

        // connectionMethodCombo
        connectionMethodCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        connectionMethodCombo.Location = new Point(85, 347);
        connectionMethodCombo.Size = new Size(160, 28);
        connectionMethodCombo.Items.AddRange(new object[] { "UUID (推荐)", "通道 15" });

        // connectButton
        connectButton.Location = new Point(330, 346);
        connectButton.Size = new Size(110, 32);
        connectButton.Text = "连接";
        connectButton.Enabled = false;
        connectButton.UseVisualStyleBackColor = true;
        connectButton.Click += ConnectButton_Click;

        // cancelButton
        cancelButton.Location = new Point(462, 346);
        cancelButton.Size = new Size(110, 32);
        cancelButton.Text = "取消";
        cancelButton.UseVisualStyleBackColor = true;
        cancelButton.Click += CancelButton_Click;

        // DevicePickerForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(584, 390);
        Font = new Font("Segoe UI", 9F);
        Controls.Add(scanButton);
        Controls.Add(statusLabel);
        Controls.Add(scanProgress);
        Controls.Add(deviceList);
        Controls.Add(connectionMethodLabel);
        Controls.Add(connectionMethodCombo);
        Controls.Add(connectButton);
        Controls.Add(cancelButton);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "选择 OPPO 耳机";
        ResumeLayout(false);
        PerformLayout();
    }
}

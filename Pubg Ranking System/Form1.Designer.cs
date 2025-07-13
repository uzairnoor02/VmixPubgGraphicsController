namespace Pubg_Ranking_System
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            Add_Tournamen_btn = new Button();
            start_btn = new Button();
            stop_btn = new Button();
            TournamentName_cmb = new ComboBox();
            Stage_cmb = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            Day_cmb = new ComboBox();
            label3 = new Label();
            Match_cmb = new ComboBox();
            label4 = new Label();
            groupBox1 = new GroupBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            button7 = new Button();
            button6 = new Button();
            groupBox2 = new GroupBox();
            button8 = new Button();
            MapName_cmb = new ComboBox();
            reload_teams_btn = new Button();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // Add_Tournamen_btn
            // 
            Add_Tournamen_btn.Location = new Point(12, 12);
            Add_Tournamen_btn.Name = "Add_Tournamen_btn";
            Add_Tournamen_btn.Size = new Size(99, 42);
            Add_Tournamen_btn.TabIndex = 0;
            Add_Tournamen_btn.Text = "Add tournament";
            Add_Tournamen_btn.UseVisualStyleBackColor = true;
            Add_Tournamen_btn.Click += Add_Tournament_btn_Click;
            // 
            // start_btn
            // 
            start_btn.Location = new Point(18, 213);
            start_btn.Name = "start_btn";
            start_btn.Size = new Size(99, 42);
            start_btn.TabIndex = 0;
            start_btn.Text = "Start Match";
            start_btn.UseVisualStyleBackColor = true;
            start_btn.Click += start_btn_Click;
            // 
            // stop_btn
            // 
            stop_btn.Location = new Point(18, 261);
            stop_btn.Name = "stop_btn";
            stop_btn.Size = new Size(99, 42);
            stop_btn.TabIndex = 0;
            stop_btn.Text = "Stop";
            stop_btn.UseVisualStyleBackColor = true;
            stop_btn.Click += stop_Click;
            // 
            // reload_teams_btn
            // 
            reload_teams_btn.Location = new Point(18, 309);
            reload_teams_btn.Name = "reload_teams_btn";
            reload_teams_btn.Size = new Size(99, 42);
            reload_teams_btn.TabIndex = 0;
            reload_teams_btn.Text = "Reload Teams";
            reload_teams_btn.UseVisualStyleBackColor = true;
            reload_teams_btn.Click += reload_teams_btn_Click;
            // 
            // TournamentName_cmb
            // 
            TournamentName_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            TournamentName_cmb.FormattingEnabled = true;
            TournamentName_cmb.Location = new Point(18, 33);
            TournamentName_cmb.Name = "TournamentName_cmb";
            TournamentName_cmb.Size = new Size(121, 23);
            TournamentName_cmb.TabIndex = 1;
            // 
            // Stage_cmb
            // 
            Stage_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Stage_cmb.FormattingEnabled = true;
            Stage_cmb.Location = new Point(18, 79);
            Stage_cmb.Name = "Stage_cmb";
            Stage_cmb.Size = new Size(121, 23);
            Stage_cmb.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(18, 15);
            label1.Name = "label1";
            label1.Size = new Size(72, 15);
            label1.TabIndex = 2;
            label1.Text = "Tournament";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(18, 61);
            label2.Name = "label2";
            label2.Size = new Size(36, 15);
            label2.TabIndex = 2;
            label2.Text = "Stage";
            // 
            // Day_cmb
            // 
            Day_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Day_cmb.FormattingEnabled = true;
            Day_cmb.Location = new Point(18, 125);
            Day_cmb.Name = "Day_cmb";
            Day_cmb.Size = new Size(121, 23);
            Day_cmb.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(18, 107);
            label3.Name = "label3";
            label3.Size = new Size(27, 15);
            label3.TabIndex = 2;
            label3.Text = "Day";
            // 
            // Match_cmb
            // 
            Match_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Match_cmb.FormattingEnabled = true;
            Match_cmb.Location = new Point(18, 172);
            Match_cmb.Name = "Match_cmb";
            Match_cmb.Size = new Size(121, 23);
            Match_cmb.TabIndex = 1;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(18, 154);
            label4.Name = "label4";
            label4.Size = new Size(41, 15);
            label4.TabIndex = 2;
            label4.Text = "Match";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(Day_cmb);
            groupBox1.Controls.Add(stop_btn);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(start_btn);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(TournamentName_cmb);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(Stage_cmb);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(Match_cmb);
            groupBox1.Location = new Point(12, 90);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(169, 316);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "MatchBox1";
            // 
            // button1
            // 
            button1.Location = new Point(21, 41);
            button1.Name = "button1";
            button1.Size = new Size(158, 23);
            button1.TabIndex = 4;
            button1.Text = "Teams to Watch";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(21, 245);
            button2.Name = "button2";
            button2.Size = new Size(158, 27);
            button2.TabIndex = 5;
            button2.Text = "RESET";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(21, 75);
            button3.Name = "button3";
            button3.Size = new Size(158, 23);
            button3.TabIndex = 6;
            button3.Text = "Match Rankings";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Location = new Point(21, 109);
            button4.Name = "button4";
            button4.Size = new Size(158, 23);
            button4.TabIndex = 7;
            button4.Text = "OverAll Rankings";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button5
            // 
            button5.Location = new Point(21, 143);
            button5.Name = "button5";
            button5.Size = new Size(158, 23);
            button5.TabIndex = 7;
            button5.Text = "Match MVP";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button7
            // 
            button7.Location = new Point(21, 177);
            button7.Name = "button7";
            button7.Size = new Size(158, 23);
            button7.TabIndex = 8;
            button7.Text = "WWCD Team";
            button7.UseVisualStyleBackColor = true;
            button7.Click += button7_Click;
            // 
            // button6
            // 
            button6.BackColor = SystemColors.ButtonShadow;
            button6.Location = new Point(21, 211);
            button6.Name = "button6";
            button6.Size = new Size(158, 23);
            button6.TabIndex = 9;
            button6.Text = "Set All";
            button6.UseVisualStyleBackColor = false;
            button6.Click += button6_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(button5);
            groupBox2.Controls.Add(button2);
            groupBox2.Controls.Add(button6);
            groupBox2.Controls.Add(button1);
            groupBox2.Controls.Add(button7);
            groupBox2.Controls.Add(button3);
            groupBox2.Controls.Add(button4);
            groupBox2.Location = new Point(203, 105);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(200, 301);
            groupBox2.TabIndex = 10;
            groupBox2.TabStop = false;
            groupBox2.Text = "groupBox2";
            // 
            // button8
            // 
            button8.Location = new Point(589, 120);
            button8.Name = "button8";
            button8.Size = new Size(75, 23);
            button8.TabIndex = 11;
            button8.Text = "button8";
            button8.UseVisualStyleBackColor = true;
            button8.Click += button8_Click;
            // 
            // MapName_cmb
            // 
            MapName_cmb.FormattingEnabled = true;
            MapName_cmb.Location = new Point(573, 90);
            MapName_cmb.Name = "MapName_cmb";
            MapName_cmb.Size = new Size(121, 23);
            MapName_cmb.TabIndex = 12;
            // 
            // reload_teams_btn
            // 
            reload_teams_btn.Location = new Point(117, 12);
            reload_teams_btn.Name = "reload_teams_btn";
            reload_teams_btn.Size = new Size(99, 42);
            reload_teams_btn.TabIndex = 0;
            reload_teams_btn.Text = "Reload Teams";
            reload_teams_btn.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 550);
            Controls.Add(MapName_cmb);
            Controls.Add(button8);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(reload_teams_btn);
            Controls.Add(Add_Tournamen_btn);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "Dashboard";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Button Add_Tournamen_btn;
        private Button start_btn;
        private Button stop_btn;
        private ComboBox TournamentName_cmb;
        private ComboBox Stage_cmb;
        private Label label1;
        private Label label2;
        private ComboBox Day_cmb;
        private Label label3;
        private ComboBox Match_cmb;
        private Label label4;
        private GroupBox groupBox1;
        private Button button1;
        private Button button2;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button7;
        private Button button6;
        private GroupBox groupBox2;
        private Button button8;
        private ComboBox MapName_cmb;
        private Button reload_teams_btn;
    }
}

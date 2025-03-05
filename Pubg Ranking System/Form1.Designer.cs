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
            Add_Tournamen_btn = new Button();
            start_btn = new Button();
            button4 = new Button();
            TournamentName_cmb = new ComboBox();
            Stage_cmb = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            Day_cmb = new ComboBox();
            label3 = new Label();
            Match_cmb = new ComboBox();
            label4 = new Label();
            groupBox1 = new GroupBox();
            groupBox1.SuspendLayout();
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
            Add_Tournamen_btn.Click += Add_Tournamen_btn_Click;
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
            // button4
            // 
            button4.Location = new Point(571, 406);
            button4.Name = "button4";
            button4.Size = new Size(99, 42);
            button4.TabIndex = 0;
            button4.Text = "Add tournament";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
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
            groupBox1.Size = new Size(169, 290);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "MatchBox1";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 550);
            Controls.Add(groupBox1);
            Controls.Add(button4);
            Controls.Add(Add_Tournamen_btn);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button Add_Tournamen_btn;
        private Button start_btn;
        private Button button4;
        private ComboBox TournamentName_cmb;
        private ComboBox Stage_cmb;
        private Label label1;
        private Label label2;
        private ComboBox Day_cmb;
        private Label label3;
        private ComboBox Match_cmb;
        private Label label4;
        private GroupBox groupBox1;
    }
}

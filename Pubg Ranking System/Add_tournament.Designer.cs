namespace Pubg_Ranking_System
{
    partial class Add_tournament
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            label2 = new Label();
            stage_name_cmb = new ComboBox();
            mySqlCommand1 = new MySqlConnector.MySqlCommand();
            label4 = new Label();
            Save = new Button();
            add_tournament_btn = new Button();
            Cancel_btn = new Button();
            tournament_name_txt = new ComboBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(39, 115);
            label1.Name = "label1";
            label1.Size = new Size(110, 15);
            label1.TabIndex = 0;
            label1.Text = "Tournament Name:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(174, 162);
            label2.Name = "label2";
            label2.Size = new Size(64, 15);
            label2.TabIndex = 2;
            label2.Text = "Add Stage:";
            // 
            // stage_name_cmb
            // 
            stage_name_cmb.FormattingEnabled = true;
            stage_name_cmb.Location = new Point(154, 197);
            stage_name_cmb.Name = "stage_name_cmb";
            stage_name_cmb.Size = new Size(280, 23);
            stage_name_cmb.TabIndex = 3;
            // 
            // mySqlCommand1
            // 
            mySqlCommand1.CommandTimeout = 0;
            mySqlCommand1.Connection = null;
            mySqlCommand1.Transaction = null;
            mySqlCommand1.UpdatedRowSource = System.Data.UpdateRowSource.None;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(54, 200);
            label4.Name = "label4";
            label4.Size = new Size(74, 15);
            label4.TabIndex = 2;
            label4.Text = "Stage Name:";
            // 
            // Save
            // 
            Save.Location = new Point(200, 254);
            Save.Name = "Save";
            Save.Size = new Size(75, 23);
            Save.TabIndex = 5;
            Save.Text = "Save";
            Save.UseVisualStyleBackColor = true;
            Save.Click += Save_Click;
            // 
            // add_tournament_btn
            // 
            add_tournament_btn.Location = new Point(442, 108);
            add_tournament_btn.Name = "add_tournament_btn";
            add_tournament_btn.Size = new Size(107, 23);
            add_tournament_btn.TabIndex = 6;
            add_tournament_btn.Text = "Add tournament";
            add_tournament_btn.UseVisualStyleBackColor = true;
            add_tournament_btn.Click += add_tournament_btn_Click;
            // 
            // Cancel_btn
            // 
            Cancel_btn.Location = new Point(281, 254);
            Cancel_btn.Name = "Cancel_btn";
            Cancel_btn.Size = new Size(75, 23);
            Cancel_btn.TabIndex = 7;
            Cancel_btn.Text = "Cancel";
            Cancel_btn.UseVisualStyleBackColor = true;
            Cancel_btn.Click += Cancel_btn_Click;
            // 
            // tournament_name_txt
            // 
            tournament_name_txt.FormattingEnabled = true;
            tournament_name_txt.Location = new Point(154, 109);
            tournament_name_txt.Name = "tournament_name_txt";
            tournament_name_txt.Size = new Size(282, 23);
            tournament_name_txt.TabIndex = 8;
            // 
            // Add_tournament
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(556, 343);
            Controls.Add(tournament_name_txt);
            Controls.Add(Cancel_btn);
            Controls.Add(add_tournament_btn);
            Controls.Add(Save);
            Controls.Add(stage_name_cmb);
            Controls.Add(label4);
            Controls.Add(label2);
            Controls.Add(label1);
            Name = "Add_tournament";
            Text = "Add_tournament";
            Load += Add_tournament_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label label2;
        private ComboBox stage_name_cmb;
        private MySqlConnector.MySqlCommand mySqlCommand1;
        private Label label4;
        private Button Save;
        private Button add_tournament_btn;
        private Button Cancel_btn;
        private ComboBox tournament_name_txt;
    }
}
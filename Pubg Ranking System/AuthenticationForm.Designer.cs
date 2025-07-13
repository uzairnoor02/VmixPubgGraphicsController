
namespace Pubg_Ranking_System
{
    partial class AuthenticationForm
    {
        private System.ComponentModel.IContainer components = null;

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
            this.lblTitle = new Label();
            this.lblInstruction = new Label();
            this.txtKey = new TextBox();
            this.btnValidate = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblTitle.Location = new Point(85, 30);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(230, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Application Authentication";
            // 
            // lblInstruction
            // 
            this.lblInstruction.AutoSize = true;
            this.lblInstruction.Location = new Point(50, 70);
            this.lblInstruction.Name = "lblInstruction";
            this.lblInstruction.Size = new Size(300, 15);
            this.lblInstruction.TabIndex = 1;
            this.lblInstruction.Text = "Please enter your authentication key to continue:";
            // 
            // txtKey
            // 
            this.txtKey.Location = new Point(50, 100);
            this.txtKey.Name = "txtKey";
            this.txtKey.Size = new Size(300, 23);
            this.txtKey.TabIndex = 2;
            this.txtKey.UseSystemPasswordChar = true;
            this.txtKey.KeyPress += new KeyPressEventHandler(this.txtKey_KeyPress);
            // 
            // btnValidate
            // 
            this.btnValidate.Location = new Point(190, 140);
            this.btnValidate.Name = "btnValidate";
            this.btnValidate.Size = new Size(75, 30);
            this.btnValidate.TabIndex = 3;
            this.btnValidate.Text = "Validate";
            this.btnValidate.UseVisualStyleBackColor = true;
            this.btnValidate.Click += new EventHandler(this.btnValidate_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new Point(275, 140);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 30);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);
            // 
            // AuthenticationForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(400, 200);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnValidate);
            this.Controls.Add(this.txtKey);
            this.Controls.Add(this.lblInstruction);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AuthenticationForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Authentication Required";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Label lblTitle;
        private Label lblInstruction;
        private TextBox txtKey;
        private Button btnValidate;
        private Button btnCancel;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VmixData.Models;
using VmixGraphicsBusiness;

namespace Pubg_Ranking_System
{
    public partial class Add_tournament : Form
    {
        List<Stage> stages = new List<Stage>();
        vmix_graphicsContext _vmix_GraphicsContext { get; set; }
        Tournament thisTournament { get; set; }
        private readonly TournamentBusiness _tournamentBusiness;
        public Add_tournament(TournamentBusiness tournamentBusiness)
        {
            InitializeComponent();
            _tournamentBusiness = tournamentBusiness;
            var tournamentnames = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
            stage_name_cmb.DataSource = _tournamentBusiness.getAllStages().Select(x => x.Name).ToList();
            tournament_name_txt.DataSource = tournamentnames;
        }

        private void Save_Click(object sender, EventArgs e)
        {
            var stage = new Stage()
            {
                Name = stage_name_cmb.Text,
            };
            var result = _tournamentBusiness.Save_Click(stage, tournament_name_txt.Text);
            if (result.Item2 == 0)
            {
                MessageBox.Show(result.Item1, "");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private async void add_tournament_btn_Click(object sender, EventArgs e)
        {
            Tournament tournament = new Tournament()
            {
                Name = tournament_name_txt.Text
            };
            var result = await _tournamentBusiness.add_tournament_btn_Click(tournament);
            MessageBox.Show(result.Item1, "");
        }

        private void Cancel_btn_Click(object sender, EventArgs e)
        {
            if (stages.Any())
            {
                var result = MessageBox.Show(
                    "There are unsaved changes. Do you want to save them before closing?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    Save_Click(sender, e);
                }
                else if (result == DialogResult.No)
                {
                    _vmix_GraphicsContext.ChangeTracker.Clear();
                    this.Close();
                }
            }
            else
            {
                this.Close();
            }
        }

        private void Add_tournament_Load(object sender, EventArgs e)
        {

        }
    }
}

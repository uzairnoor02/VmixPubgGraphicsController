using System.Collections.Generic;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.MatchBusiness;

namespace Pubg_Ranking_System
{
    public partial class Form1 : Form
    {
        Add_tournament _Add_tournament { get; set; }
        GetLiveData _getLiveData { get; set; }
        private TournamentBusiness _tournamentBusiness { get; set; }
        private LiveStatsBusiness _liveStatsBusiness { get; set; }
        public Form1(Add_tournament add_Tournament, GetLiveData getLiveData, LiveStatsBusiness liveStatsBusiness,TournamentBusiness tournamentBusiness)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _Add_tournament = add_Tournament;
            _getLiveData = getLiveData;
            InitializeComponent();
            _tournamentBusiness = tournamentBusiness;
            var tournamentnames = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
            Stage_cmb.DataSource = _tournamentBusiness.getAllStages().Select(x => x.Name).ToList();
            TournamentName_cmb.DataSource = tournamentnames;
            var days = new List<string>();
            days.Add("1"); days.Add("2"); days.Add("3"); days.Add("4"); days.Add("5"); days.Add("6"); days.Add("7"); days.Add("8");
            var matches = new List<string>();
            matches.Add("1"); matches.Add("2"); matches.Add("3"); matches.Add("4"); matches.Add("5"); matches.Add("6"); matches.Add("7"); matches.Add("8");
            Day_cmb.DataSource = days;
            Match_cmb.DataSource = matches;
        }

        private void Add_Tournamen_btn_Click(object sender, EventArgs e)
        {
            _Add_tournament.Show();
        }

        private async void start_btn_Click(object sender, EventArgs e)
        {
           var result=await  _tournamentBusiness.add_match(TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text);
            if (result.Item2 == 0)
            {

                await _getLiveData.FetchAndPostData(result.Item3);
            }
            else
            {
                if (MessageBox.Show(result.Item1, "", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    await _getLiveData.FetchAndPostData(result.Item3);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Define the output folder path
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "resources");

            try
            {
                // Check if the folder exists and delete it
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true); // 'true' ensures all contents are deleted
                    Console.WriteLine($"Deleted folder: {outputFolder}");
                }
            }
            catch (Exception ex)
            {
                // Log or handle exceptions
                Console.WriteLine($"Error deleting folder: {ex.Message}");
            }
        }
        private async void button4_Click(object sender, EventArgs e)
        {
            try
            {
                // Run the IsEliminatedAsync method and await its result
                await _liveStatsBusiness.IsEliminatedAsync("AS I8", 4, true, 5, 1);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}

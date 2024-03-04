using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GithubStealthStealer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        string gitTestRepo = "https://github.com/savvamadar/Unity3D-Simple-Water-Buoyancy-Script";

        Dictionary<string, TextBox> linkType_TextBox = new Dictionary<string, TextBox>();

        string connectionType = "";

        CloneProgress progress;

        private bool DeleteDir(string dirPath)
        {
            if (Directory.Exists(dirPath))
            {
                try
                {
                    runOneOff("rmdir /s/q " + dirPath);
                }
                catch (System.UnauthorizedAccessException)
                {
                    Log("Insufficient privileges to delete - try running as Admin");
                    return false;
                }
                catch (Exception error)
                {
                    Log(error);
                    return false;
                }
            }

            return true;
        }

        private bool DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    runOneOff("del /q " + filePath);
                }
                catch (System.UnauthorizedAccessException)
                {
                    Log("Insufficient privileges to delete - try running as Admin");
                    return false;
                }
                catch (Exception error)
                {
                    Log(error);
                    return false;
                }
            }

            return true;
        }

        private void DeleteAllButGit(string dirPath)
        {
            if (Directory.Exists(dirPath))
            {
                // Process all directories and files in the directory
                foreach (var folder in Directory.GetDirectories(dirPath))
                {
                    // Check if the directory is the .git folder
                    if (Path.GetFileName(folder).Equals(".git", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DeleteDir(folder);
                }

                // Delete all files in the directory
                foreach (var file in Directory.GetFiles(dirPath))
                {
                    DeleteFile(file);
                }
            }
        }

        private void CopyAllButGit(string sourceRootFolder, string destinationRootFolder)
        {
            if (Directory.Exists(sourceRootFolder) && Directory.Exists(destinationRootFolder))
            {
                foreach (var folder in Directory.GetDirectories(sourceRootFolder))
                {
                    // Check if the directory is the .git folder
                    if (Path.GetFileName(folder).Equals(".git", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    runOneOff($"xcopy \"{folder}\" \"{Path.Combine(destinationRootFolder, Path.GetFileName(folder))}\" /E /I /Q");
                }

                foreach (var file in Directory.GetFiles(sourceRootFolder))
                {
                    if (checkBox1.Checked && Path.GetFileName(file).ToLower() == "readme.md")
                    {
                        continue;
                    }

                    runOneOff($"copy /Y \"{file}\" \"{Path.Combine(destinationRootFolder, Path.GetFileName(file))}\"");
                }
            }
        }

        //TODO:
        //git cat-file -p $shortHashID | can be used to see if it was a merge
        //create branches using above info

        public class CloneProgress
        {
            public string masterLink = "";
            public int currentStep = 0;
            public int commitsCopied = 0;
            public List<string> commitHashes = new List<string>();

            public bool includeReadMe = true;

            public bool useAutomate = false;

            public float minutesLessThan = 0;
            public float speedLessThan = 100.0f;

            public float speedInbetween = 100.0f;

            public float minutesMoreThan = 999999999;
            public float speedMoreThan = 100.0f;

            public int startHour = 9;
            public int endHour = 23;
        }

        private bool DeleteLocalTestRepo()
        {
            return DeleteDir(GetRepoName());
        }

        public Random rnd = new Random();

        int randomStartMinute = 0;
        int randomEndMinute = 0;
        private void Form1_Load(object sender, EventArgs e)
        {

            textBox5.Validating += TextBox5_Validating;
            textBox6.Validating += TextBox6_Validating;
            textBox7.Validating += TextBox7_Validating;
            textBox8.Validating += TextBox8_Validating;
            textBox9.Validating += TextBox9_Validating;

            textBox10.Validating += TextBox10_Validating;
            textBox11.Validating += TextBox11_Validating;

            linkType_TextBox["http"] = textBox2;
            linkType_TextBox["ssh"] = textBox3;

            checkBox2.Checked = false;

            checkBox2_CheckedChanged(null, null);

            if (!CheckGitInit())
            {
                return;
            }

            textBox1.Text = gitTestRepo;

            connectionType = GetGitProtocol();


            if (connectionType == "")
            {
                Log("Unable to clone test repo, check that git has credentials");
                return;
            }

            textBox1.ReadOnly = true;



            textBox1.Text = "";
            textBox2.Text = "";
            textBox3.Text = "";

            LoadSaveStateOnStart();

            randomStartMinute = rnd.Next(0, 60);
            randomEndMinute = rnd.Next(0, 59);
            

            if (progress.startHour == progress.endHour)
            {
                randomStartMinute = rnd.Next(0, 15);
                randomEndMinute = rnd.Next(45, 59);
            }


            if (randomStartMinute < 10)
            {
                label17.Text = ":0" + randomStartMinute + " local time and ";
            }
            else
            {
                label17.Text = ":" + randomStartMinute + " local time and ";
            }

            if (randomEndMinute < 10)
            {
                label18.Text = ":0" + randomEndMinute + " local time";
            }
            else
            {
                label18.Text = ":" + randomEndMinute + " local time";
            }
        }

        private bool CheckGitInit()
        {
            string checkGitCommand = runOneOff("git --version");
            bool hasGit = !(checkGitCommand.ToLower().IndexOf("recognized") != -1 || checkGitCommand.ToLower().IndexOf("git version") == -1);

            Log("git --version = " + checkGitCommand.Trim());

            if (!hasGit)
            {
                Log("Unable to locate git.");
                return false;
            }

            string checkGitLFSInitializedCommand = runOneOff("git lfs install");
            bool lfsInitialized = checkGitLFSInitializedCommand.ToLower().IndexOf(" initialized") != -1;

            Log("git lfs install = " + checkGitLFSInitializedCommand.Trim());

            if (!lfsInitialized)
            {
                Log("Unable to initialize git lfs.");
                return false;
            }

            string checkGitCLICommand = runOneOff("gh --version");
            bool hasGitCLI = !(checkGitCLICommand.ToLower().IndexOf("recognized") != -1);

            Log("gh --version = " + checkGitCLICommand.Trim());

            if (!hasGitCLI)
            {
                Log("Please install the git cli: https://github.com/cli/cli");
                return false;
            }

            string checkGHLoggedIn = runOneOff("gh auth status"); //gh auth login
            bool isLoggedIn = (checkGHLoggedIn.ToLower().IndexOf("logged in to github.com") != -1);

            if (!isLoggedIn)
            {
                Log("Please log into gh: $ gh auth login");
                return false;
            }

            return true;
        }

        private string GetGitProtocol()
        {
            string[] protocolOrder = new string[] { "ssh", "http" };

            string returnedConnectionType = "";

            if (!DeleteLocalTestRepo())
            {
                return "";
            }

            for (int i = 0; i < protocolOrder.Length; i++)
            {
                Log("Checking " + protocolOrder[i] + " Availability...");
                string gitCloneResults = runOneOff("git clone " + linkType_TextBox[protocolOrder[i]].Text);
                string[] gitCloneLines = gitCloneResults.Split('\n');

                bool allOk = true;
                for (int j = 0; j < gitCloneLines.Length; j++)
                {
                    if (gitCloneResults.ToLower().IndexOf("error:") >= 0 || gitCloneResults.ToLower().IndexOf("fatal:") >= 0)
                    {
                        allOk = false;
                        break;
                    }
                }

                if (!allOk)
                {
                    Log(protocolOrder[i] + " Unavailable");
                }
                else
                {
                    returnedConnectionType = protocolOrder[i];
                    Log(protocolOrder[i] + " Available");
                    break;
                }

                DeleteLocalTestRepo();

            }

            if (!DeleteLocalTestRepo())
            {
                return "";
            }

            return returnedConnectionType;
        }

        private void GenerateLinkToSSH()
        {
            string[] urlSplit = textBox1.Text.Split('/');
            if (urlSplit.Length >= 5)
            {
                linkType_TextBox["ssh"].Text = "git@github.com:" + urlSplit[3] + "/" + urlSplit[4] + ".git";
            }
        }

        private void GenerateLinkToHTTP()
        {
            linkType_TextBox["http"].Text = textBox1.Text + ".git";
        }

        private string GetRepoName()
        {
            string[] urlSplit = textBox1.Text.Split('/');
            return urlSplit[urlSplit.Length - 1];
        }

        private void Log(object o, bool newline = true)
        {
            Log(o.ToString(), newline);
        }

        private void Log(string s, bool newline = true)
        {
            richTextBox1.Text += s + (newline ? "\n" : "");
        }

        private void loadSaveState()
        {
            if (File.Exists("save.json"))
            {
                progress = JsonConvert.DeserializeObject<CloneProgress>(File.ReadAllText("save.json"));
            }
            else
            {
                progress = new CloneProgress();
            }
        }

        private int GetSanitizedValue(TextBox tb, int defaultValue)
        {
            if (tb.Text.Trim().Length == 0)
            {
                return defaultValue;
            }

            try
            {
                return int.Parse(tb.Text);
            }
            catch
            {
                return defaultValue;
            }
        }

        private float GetSanitizedValue(TextBox tb, float defaultValue)
        {
            if(tb.Text.Trim().Length == 0)
            {
                return defaultValue;
            }

            try
            {
                return float.Parse(tb.Text);
            }
            catch
            {
                return defaultValue;
            }
        }

        private double GetSanitizedValue(TextBox tb, double defaultValue)
        {
            if (tb.Text.Trim().Length == 0)
            {
                return defaultValue;
            }

            try
            {
                return double.Parse(tb.Text);
            }
            catch
            {
                return defaultValue;
            }
        }

        private void saveSaveState()
        {
            if (!enableSaveStateSaving || progress.masterLink.Trim().Length == 0)
            {
                return;
            }

            progress.includeReadMe = !checkBox1.Checked;

            if (checkBox2.Checked)
            {
                progress.minutesLessThan = GetSanitizedValue(textBox5, 0.0f);
                progress.speedLessThan = GetSanitizedValue(textBox6, 100.0f);

                progress.speedInbetween = GetSanitizedValue(textBox7, 100.0f);

                progress.minutesMoreThan = GetSanitizedValue(textBox9, 999999999.0f);
                progress.speedMoreThan = GetSanitizedValue(textBox8, 100.0f);

                progress.startHour = GetSanitizedValue(textBox10, 9);
                progress.startHour = Math.Max(0, Math.Min(24, progress.startHour));

                progress.endHour = GetSanitizedValue(textBox11, 23);
                progress.endHour = Math.Max(0, Math.Min(24, progress.endHour));
            }

            progress.useAutomate = checkBox2.Checked;

            File.WriteAllText("save.json",JsonConvert.SerializeObject(progress));
        }

        private string runOneOff(string cmd)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C {cmd}";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            process.StartInfo = startInfo;
            process.Start();
            string outputRegular = "";
            string outputError = "";

            while (!process.HasExited)
            {
                outputRegular += process.StandardOutput.ReadToEnd();
                outputError += process.StandardError.ReadToEnd();
            }

            string result = (outputRegular.Trim() == "") ? outputError : (outputRegular + "\n" + outputError);

            return result;
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            GenerateLinkToSSH();
            GenerateLinkToHTTP();
        }

        private string GenerateLocationOriginal(string branch)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("original", branch));
        }

        private string GenerateLocationClone(string branch)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("clone", branch));
        }

        private bool enableSaveStateSaving = false;
        private void LoadSaveStateOnStart()
        {
            loadSaveState();

            if (progress.currentStep > 0)
            {
                textBox1.Text = progress.masterLink;
                textBox1.ReadOnly = true;
                button1_Click(this, null);
            }
            else
            {
                textBox1.ReadOnly = false;
            }

            checkBox1.Checked = !progress.includeReadMe;


            textBox5.Text = progress.minutesLessThan.ToString("F2");
            textBox6.Text = progress.speedLessThan.ToString("F2");

            textBox7.Text = progress.speedInbetween.ToString("F2");

            textBox9.Text = progress.minutesMoreThan.ToString("F2");
            textBox8.Text = progress.speedMoreThan.ToString("F2");

            textBox10.Text = progress.startHour.ToString("F0");
            textBox11.Text = progress.endHour.ToString("F0");

            checkBox2.Checked = progress.useAutomate;

            checkBox2_CheckedChanged(null, null);

            enableSaveStateSaving = true;

            timer1.Enabled = true;
        }

        //git show -s --format="%ct" a0e2327
        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;

            if(progress.currentStep == 0)
            {
                progress.currentStep = 0;
                progress.masterLink = textBox1.Text;
            }

            textBox1.ReadOnly = true;

            if (!Directory.Exists("original/main_branch"))
            {
                Directory.CreateDirectory("original/main_branch");
            }
            if (!Directory.Exists("clone/main_branch"))
            {
                Directory.CreateDirectory("clone/main_branch");
            }

            if (progress.currentStep == 0)
            {
                DeleteDir(Path.Combine(Path.Combine("original", "main_branch"),GetRepoName()));
                Log("Cloning " + GetRepoName()+" | may hang...");
                Log(runOneOff($"cd {GenerateLocationOriginal("main_branch")} && git clone " + linkType_TextBox[connectionType].Text));
                progress.currentStep = 1;
                saveSaveState();
            }

            if(progress.currentStep == 1)
            {
                string[] commitShortHashes = runOneOff($"cd {Path.Combine(GenerateLocationOriginal("main_branch"), GetRepoName())} && git rev-list --abbrev-commit HEAD").Trim().Split('\n');

                bool allGoodCommits = true;
                progress.commitHashes.Clear();
                for(int i=0; i < commitShortHashes.Length; i++)
                {
                    if(commitShortHashes[i].Trim() != "")
                    {
                        if(commitShortHashes[i].Trim().Length != 7)
                        {
                            allGoodCommits = false;
                            Log("'" + commitShortHashes[i] + "' -- bad commit hash");
                            progress.commitHashes.Clear();
                            break;
                        }
                        else
                        {
                            progress.commitHashes.Add(commitShortHashes[i]);
                        }
                    }
                }

                if (allGoodCommits)
                {
                    Log("commit count: " + progress.commitHashes.Count);
                    progress.currentStep = 2;
                    saveSaveState();
                }
            }

            if(progress.currentStep == 2)
            {
                StageCommitIndex(0);
            }
            else if(progress.currentStep == 3)
            {
                StageCommitIndex(progress.commitsCopied);
            }
        }
        //git cat-file -p 3c493c5

        private string[] getCommitHashes(int index)
        {
            string currentCommit = "";
            string nextCommit = "";

            /*if (progress != null && progress.commitHashes != null && index >= 0)
            {
                if (index > 0 && (progress.commitHashes.Count - (1 + index) >= 0))
                {
                    nextCommit = progress.commitHashes[progress.commitHashes.Count - (1 + index)];
                }

                if (index > 0 && ((progress.commitHashes.Count - index) > 0 && (progress.commitHashes.Count - index) < progress.commitHashes.Count))
                {
                    currentCommit = progress.commitHashes[progress.commitHashes.Count - index];
                }
            }*/

            if (progress != null && progress.commitHashes != null && index >= 0)
            {
                if (progress.commitHashes.Count - (1 + index) >= 0)
                {
                    nextCommit = progress.commitHashes[progress.commitHashes.Count - (1 + index)];
                }

                if (index > 0)
                {
                    currentCommit = progress.commitHashes[progress.commitHashes.Count - index];
                }
            }

            return new string[] { currentCommit, nextCommit };
        }

        int commitIndex = -1;
        string[] commits = new string[] { "", "" };
        private void StageCommitIndex(int index)
        {
            commitIndex = index;

            commits = getCommitHashes(commitIndex);

            checkBox2_CheckedChanged(null, null);

            string currentCommit = commits[0];
            string nextCommit = commits[1];

            UpdateUI("main_branch", currentCommit, nextCommit);

            if (nextCommit != "")
            {
                button2.Text = "Clone Commit #"+(index+1);
                button2.Enabled = true;
            }
            else
            {
                button2.Text = "Clean up";
                button2.Enabled = true;
            }

            didFirstTimerCaclulation = false;
            timerSecondTicksRemaining = 0;
            isCommitting = false;

        }

        private double CalculateTimeBetweenCommits(string branch, string currentCommit, string nextCommit, string LOC) {

            //Log("CTBC: " + commitIndex + " | " + currentCommit + " | " + nextCommit + " | " + LOC);

            if (currentCommit == "" || nextCommit == "")
            {
                return 0.0;
            }

            string unixCurrent = runOneOff($"cd {Path.Combine(GenerateLocationOriginal(branch), GetRepoName())} && git show -s --format=\"%ct\" "+currentCommit);
            string unixNext = runOneOff($"cd {Path.Combine(GenerateLocationOriginal(branch), GetRepoName())} && git show -s --format=\"%ct\" " + nextCommit);

            System.DateTime epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            System.DateTime dateTimeCurrent = epoch.AddSeconds(long.Parse(unixCurrent));
            System.DateTime dateTimeNext = epoch.AddSeconds(long.Parse(unixNext));

            TimeSpan difference = dateTimeNext - dateTimeCurrent;

            return difference.TotalMinutes;
        }

        private string GetCommitCount()
        {
            return " --- ("+(progress.commitsCopied + "/" + progress.commitHashes.Count)+") commits done";
        }

        double commitTiming = 0;
        private void UpdateUI(string branch, string currentCommit, string nextCommit)
        {
            if (currentCommit == "")
            {
                commitTiming = 0;
                label5.Text = nextCommit + " is the first commit | " + GetCommitCount();
            }
            else if (nextCommit == "")
            {
                commitTiming = 0;
                label5.Text = currentCommit + " was the final commit | " + GetCommitCount();
            }
            else
            {
                commitTiming = CalculateTimeBetweenCommits(branch, currentCommit, nextCommit, "UpdateUI");
                label5.Text = "It took the original author " + commitTiming.ToString("F2") + " minutes to commit from " + currentCommit + " to " + nextCommit + " | " + GetCommitCount();
            }

            if (nextCommit != "")
            {
                textBox4.Text = GetOriginalCommitMessage(branch, nextCommit);
                textBox4.ReadOnly = false;
            }
            else
            {
                textBox4.Text = "";
                textBox4.ReadOnly = true;
            }
        }
        
        private string GetOriginalCommitMessage(string branch, string commit)
        {
            //git show -s --format=%B COMMIT_HASH
            return runOneOff($"cd {Path.Combine(GenerateLocationOriginal(branch), GetRepoName())} && git show -s --format=%B " + commit).Trim();
        }

        private void ClearOutLog()
        {
            richTextBox1.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {

            if(commitIndex == progress.commitHashes.Count)
            {
                button2.Enabled = false;
                Log("Cleaning up...");

                Log("Delete original...");
                DeleteDir(Path.Combine(Directory.GetCurrentDirectory(), "original"));

                Log("Delete clone...");
                DeleteDir(Path.Combine(Directory.GetCurrentDirectory(), "clone"));

                Log("Delete save...");

                if (File.Exists("save.json"))
                {
                    File.Delete("save.json");
                }

                button2.Text = "Restart app";

                return;
            }

            ClearOutLog();
            button2.Enabled = false;

            isCommitting = true;

            if (commitIndex == 0)
            {
                if (!Directory.Exists(Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())))
                {
                    Directory.CreateDirectory(Path.Combine(GenerateLocationClone("main_branch"), GetRepoName()));
                }

                Log("Making project " + GetRepoName());
                Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git init").Trim());
                Log("Creating private GH Repo: " + GetRepoName());

                string myRepoUrl = runOneOff($"gh repo create {GetRepoName()} --private").Trim();

                Log("Result: "+ myRepoUrl);

                bool createdRepo = false;

                if(myRepoUrl.IndexOf("http") == 0 || myRepoUrl.IndexOf("git@") == 0)
                {
                    createdRepo = true;
                }
                else
                {
                    if(myRepoUrl.IndexOf("Name already exists") != -1)
                    {
                        Log("Deleting old repo: " + GetRepoName());
                        Log(runOneOff($"gh repo delete {GetRepoName()} --yes").Trim());
                        myRepoUrl = runOneOff($"gh repo create {GetRepoName()} --private").Trim();
                    }

                    if (myRepoUrl.IndexOf("http") == 0 || myRepoUrl.IndexOf("git@") == 0)
                    {
                        createdRepo = true;
                        
                    }
                    else
                    {
                        Log("Not an HTTP url for the repo upstream");
                        return;
                    }
                }

                if (createdRepo)
                {
                    Log("set branch main");
                    Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git branch -M main").Trim());
                    Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git remote add origin {myRepoUrl}").Trim());
                }
                else
                {
                    Log("Unable to create repo in github");
                    return;
                }

            }

            Log("Clearing out clone folder...");
            DeleteAllButGit(Path.Combine(GenerateLocationClone("main_branch"), GetRepoName()));
            Log("Setting original to: "+ progress.commitHashes[progress.commitHashes.Count - (1 + progress.commitsCopied)]);//progress.commitsCopied
            Log(runOneOff($"cd {Path.Combine(GenerateLocationOriginal("main_branch"), GetRepoName())} && git reset --hard "+ progress.commitHashes[progress.commitHashes.Count - (1 + progress.commitsCopied)]).Trim());
            Log("Copying all but git to clone");
            CopyAllButGit(Path.Combine(GenerateLocationOriginal("main_branch"), GetRepoName()), Path.Combine(GenerateLocationClone("main_branch"), GetRepoName()));
            Log("Comitting to your github...");
            Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git add .").Trim());
            Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git commit -m \"{textBox4.Text.Trim()}\"").Trim());
            Log(runOneOff($"cd {Path.Combine(GenerateLocationClone("main_branch"), GetRepoName())} && git push -u origin main").Trim());

            

            progress.currentStep = 3;
            progress.commitsCopied += 1;
            saveSaveState();
            StageCommitIndex(progress.commitsCopied);
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            textBox5.ReadOnly = !checkBox2.Checked;
            textBox6.ReadOnly = !checkBox2.Checked;

            textBox7.ReadOnly = !checkBox2.Checked;

            textBox8.ReadOnly = !checkBox2.Checked;
            textBox9.ReadOnly = !checkBox2.Checked;

            textBox10.ReadOnly = !checkBox2.Checked;
            textBox11.ReadOnly = !checkBox2.Checked;

            if (checkBox2.Checked)
            {
                if (commits[1] == "")
                {
                    if (commits[0] == "")
                    {
                        if (progress.masterLink == "")
                        {
                            label15.Text = "Nothing to automate (yet) - Must start first";
                        }
                        else
                        {
                            label15.Text = "Uh you might as well delete everything and start again...";
                        }
                    }
                    else
                    {
                        label15.Text = "Awaiting user cleanup";
                    }
                }
            }
            else
            {
                label15.Text = "Automate disabled/ paused";
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            saveSaveState();
        }

        private void TextBox5_Validating(object sender, CancelEventArgs e)
        {
            progress.minutesLessThan = GetSanitizedValue(textBox5, 0.0f);
            textBox5.Text = progress.minutesLessThan.ToString("F2");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox6_Validating(object sender, CancelEventArgs e)
        {
            progress.speedLessThan = GetSanitizedValue(textBox6, 100.0f);
            textBox6.Text = progress.speedLessThan.ToString("F2");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox7_Validating(object sender, CancelEventArgs e)
        {
            progress.speedInbetween = GetSanitizedValue(textBox7, 100.0f);
            textBox7.Text = progress.speedInbetween.ToString("F2");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox9_Validating(object sender, CancelEventArgs e)
        {
            progress.minutesMoreThan = GetSanitizedValue(textBox9, 999999999.0f);
            textBox9.Text = progress.minutesMoreThan.ToString("F2");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox8_Validating(object sender, CancelEventArgs e)
        {
            progress.speedMoreThan = GetSanitizedValue(textBox8, 100.0f);
            textBox8.Text = progress.speedMoreThan.ToString("F2");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox10_Validating(object sender, CancelEventArgs e)
        {
            progress.startHour = GetSanitizedValue(textBox10, 9);
            progress.startHour = Math.Max(0, Math.Min(24, progress.startHour));

            textBox10.Text = progress.startHour.ToString("F0");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        private void TextBox11_Validating(object sender, CancelEventArgs e)
        {
            progress.endHour = GetSanitizedValue(textBox11, 23);
            progress.endHour = Math.Max(0, Math.Min(24, progress.endHour));

            textBox11.Text = progress.endHour.ToString("F0");
            if (!checkBox2.Checked)
            {
                return;
            }
            saveSaveState();
        }

        bool didFirstTimerCaclulation = false;
        int timerSecondTicksRemaining = 0;

        bool isCommitting = false;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (isCommitting)
            {
                return;
            }

            label19.Text = " -- "+DateTime.Now.ToString("HH:mm");

            string currentCommit = commits[0];
            string nextCommit = commits[1];

            
            int cHour = int.Parse(DateTime.Now.ToString("HH"));
            int cMinute = int.Parse(DateTime.Now.ToString("HH:mm").Split(':')[1]);
            bool timeIsGood = false;


            //9 & 15
            if (progress.startHour <= progress.endHour)
            {
                if (progress.startHour <= cHour && cHour <= progress.endHour)
                {
                    if (progress.startHour == cHour)
                    {
                        timeIsGood = (cMinute >= randomStartMinute);
                    }
                    else if (progress.endHour == cHour)
                    {
                        timeIsGood = (cMinute <= randomEndMinute);
                    }
                    else
                    {
                        timeIsGood = true;
                    }
                }
            }
            else//15 & 9
            {
                if(progress.startHour <= cHour || cHour <= progress.endHour)
                {
                    if (progress.startHour == cHour)
                    {
                        timeIsGood = (cMinute >= randomStartMinute);
                    }
                    else if (progress.endHour == cHour)
                    {
                        timeIsGood = (cMinute <= randomEndMinute);
                    }
                    else
                    {
                        timeIsGood = true;
                    }
                }
            }


            if (!timeIsGood)
            {
                label19.Text = " --- Waiting for local time to be between limits: " + DateTime.Now.ToString("HH:mm");
                return;
            }
            else
            {
                label19.Text = " --- local time within limit: " + DateTime.Now.ToString("HH:mm");
            }

            timerSecondTicksRemaining -= 1;
            if (checkBox2.Checked && enableSaveStateSaving)
            {
                
                label15.Text = timerSecondTicksRemaining + " seconds before auto commit";
                if (timerSecondTicksRemaining <= 0)
                {
                    if (commits[1] != "")
                    {
                        if (didFirstTimerCaclulation)
                        {
                            button2_Click(null, null);
                        }

                        timerSecondTicksRemaining = Math.Max(0, (int)(commitTiming * 60.0));

                        //Log("OG M: " + commitTiming);
                        //Log("OG S: " + ((int)(commitTiming * 60.0)));
                        Log("Calculated seconds to wait: " + timerSecondTicksRemaining);


                        if (timerSecondTicksRemaining < (GetSanitizedValue(textBox5, 0f) * 60.0))
                        {
                            Log(timerSecondTicksRemaining+"s < "+ (GetSanitizedValue(textBox5, 0f) * 60.0)+"s -- using "+((GetSanitizedValue(textBox6, 100f)))+"% of the original commit time");
                            timerSecondTicksRemaining = (int)(timerSecondTicksRemaining * (GetSanitizedValue(textBox6, 100f) / 100.0));
                        }
                        else if (timerSecondTicksRemaining > (GetSanitizedValue(textBox9, 999999999f) * 60.0))
                        {
                            Log(timerSecondTicksRemaining + "s > " + (GetSanitizedValue(textBox9, 999999999f) * 60.0) + "s -- using " + ((GetSanitizedValue(textBox8, 100f))) + "% of the original commit time");
                            timerSecondTicksRemaining = (int)(timerSecondTicksRemaining * (GetSanitizedValue(textBox8, 100f) / 100.0));
                        }
                        else
                        {
                            Log("ELSE -- using " + ((GetSanitizedValue(textBox7, 100f))) + "% of the original commit time");
                            timerSecondTicksRemaining = (int)(timerSecondTicksRemaining * (GetSanitizedValue(textBox7, 100f) / 100.0));
                        }

                        timerSecondTicksRemaining = Math.Max(rnd.Next(2, 14), timerSecondTicksRemaining);

                        Log("Actual seconds to wait: " + timerSecondTicksRemaining);

                        didFirstTimerCaclulation = true;
                    }
                    else
                    {
                        if (currentCommit == "")
                        {
                            if (progress.masterLink == "")
                            {
                                label15.Text = "Nothing to automate (yet) - Must start first";
                            }
                            else
                            {
                                label15.Text = "Uh you might as well delete everything and start again...";
                            }
                        }
                        else
                        {
                            label15.Text = "No further commits available";
                        }
                    }
                }
            }
        }
    }
}

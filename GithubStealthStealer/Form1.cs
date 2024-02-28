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
        }

        private bool DeleteLocalTestRepo()
        {
            return DeleteDir(GetRepoName());
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            linkType_TextBox["http"] = textBox2;
            linkType_TextBox["ssh"] = textBox3;

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

        private void saveSaveState()
        {
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

        int commitIndex = -1;
        private void StageCommitIndex(int index)
        {
            commitIndex = index;

            string currentCommit = "";
            string nextCommit = "";
            if (progress.commitHashes.Count - (1 + index) >= 0)
            {
                nextCommit = progress.commitHashes[progress.commitHashes.Count - (1 + index)];
            }

            if (index > 0)
            {
                currentCommit = progress.commitHashes[progress.commitHashes.Count - index];
            }

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

            //Log(CalculateTimeBetweenCommits("main_branch", progress.commitHashes[progress.commitHashes.Count-1], progress.commitHashes[progress.commitHashes.Count - 2]));
        }

        private string CalculateTimeBetweenCommits(string branch, string currentCommit, string nextCommit) {
            string unixCurrent = runOneOff($"cd {Path.Combine(GenerateLocationOriginal(branch), GetRepoName())} && git show -s --format=\"%ct\" "+currentCommit);
            string unixNext = runOneOff($"cd {Path.Combine(GenerateLocationOriginal(branch), GetRepoName())} && git show -s --format=\"%ct\" " + nextCommit);

            System.DateTime epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            System.DateTime dateTimeCurrent = epoch.AddSeconds(long.Parse(unixCurrent));
            System.DateTime dateTimeNext = epoch.AddSeconds(long.Parse(unixNext));

            TimeSpan difference = dateTimeNext - dateTimeCurrent;

            return difference.TotalMinutes.ToString();
        }

        private string GetCommitCount()
        {
            return " --- ("+(progress.commitsCopied + "/" + progress.commitHashes.Count)+") commits done";
        }

        private void UpdateUI(string branch, string currentCommit, string nextCommit)
        {
            if (currentCommit == "")
            {
                label5.Text = nextCommit + " is the first commit | " + GetCommitCount();
            }
            else if (nextCommit == "")
            {
                label5.Text = currentCommit + " was the final commit | " + GetCommitCount();
            }
            else
            {
                label5.Text = "It took the original author " + CalculateTimeBetweenCommits(branch, currentCommit, nextCommit) + " minutes to commit from " + currentCommit + " to " + nextCommit + " | " + GetCommitCount();
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

            if(commitIndex == 0)
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
    }
}

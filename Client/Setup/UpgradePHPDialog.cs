//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

//#define VSDesigner

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.Management.Client.Win32;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Setup
{

    /// <summary>
    /// Updates the active PHP version to the latest patch release of its branch, side by side.
    /// </summary>
    internal sealed class UpgradePHPDialog :
#if VSDesigner
        Form
#else
        TaskForm
#endif
    {
        private PHPModule _module;
        private bool _isLocalConnection;

        private ManagementPanel _contentPanel;
        private Label _currentNameLabel;
        private Label _currentValueLabel;
        private Label _availableNameLabel;
        private Label _availableValueLabel;
        private Label _descriptionLabel;
        private Label _folderLabel;
        private TextBox _folderTextBox;
        private Button _browseButton;

        private PHPRelease _update;
        private bool _busy;
        private string _folderToInstall;

        public UpgradePHPDialog(PHPModule module, bool isLocalConnection) : base(module)
        {
            _module = module;
            _isLocalConnection = isLocalConnection;
            InitializeComponent();
            InitializeUI();
        }

        protected override bool CanAccept
        {
            get
            {
                return !_busy && _update != null && !String.IsNullOrEmpty(_folderTextBox.Text.Trim());
            }
        }

        protected override bool CanShowHelp
        {
            get
            {
                return false;
            }
        }

        private void InitializeComponent()
        {
            this._currentNameLabel = new Label();
            this._currentValueLabel = new Label();
            this._availableNameLabel = new Label();
            this._availableValueLabel = new Label();
            this._descriptionLabel = new Label();
            this._folderLabel = new Label();
            this._folderTextBox = new TextBox();
            this._browseButton = new Button();
            this.SuspendLayout();

            this._currentNameLabel.AutoSize = true;
            this._currentNameLabel.Location = new System.Drawing.Point(0, 10);
            this._currentNameLabel.Text = Resources.UpgradePHPDialogCurrent;

            this._currentValueLabel.Location = new System.Drawing.Point(130, 10);
            this._currentValueLabel.Size = new System.Drawing.Size(330, 16);

            this._availableNameLabel.AutoSize = true;
            this._availableNameLabel.Location = new System.Drawing.Point(0, 32);
            this._availableNameLabel.Text = Resources.UpgradePHPDialogAvailable;

            this._availableValueLabel.Location = new System.Drawing.Point(130, 32);
            this._availableValueLabel.Size = new System.Drawing.Size(330, 48);
            this._availableValueLabel.Text = Resources.UpgradePHPDialogChecking;

            this._descriptionLabel.Location = new System.Drawing.Point(0, 85);
            this._descriptionLabel.Size = new System.Drawing.Size(460, 60);
            this._descriptionLabel.Text = Resources.UpgradePHPDialogDescription;

            this._folderLabel.AutoSize = true;
            this._folderLabel.Location = new System.Drawing.Point(0, 150);
            this._folderLabel.Text = Resources.UpgradePHPDialogFolder;

            this._folderTextBox.Location = new System.Drawing.Point(0, 167);
            this._folderTextBox.Size = new System.Drawing.Size(427, 20);
            this._folderTextBox.TextChanged += new EventHandler(OnFolderTextBoxTextChanged);

            this._browseButton.Location = new System.Drawing.Point(433, 165);
            this._browseButton.Size = new System.Drawing.Size(27, 23);
            this._browseButton.Text = Resources.CommonBrowseButton;
            this._browseButton.UseVisualStyleBackColor = true;
            this._browseButton.Click += new EventHandler(OnBrowseButtonClick);

            this.ClientSize = new System.Drawing.Size(500, 280);
            this.Name = "UpgradePHPDialog";
            this.ResumeLayout(false);
#if VSDesigner
            this.PerformLayout();
#endif
        }

        private void InitializeUI()
        {
            _contentPanel = new ManagementPanel();
            _contentPanel.SuspendLayout();

            _browseButton.Visible = _isLocalConnection;
            _folderTextBox.Enabled = false;
            _browseButton.Enabled = false;

            _contentPanel.Location = new System.Drawing.Point(0, 0);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(_currentNameLabel);
            _contentPanel.Controls.Add(_currentValueLabel);
            _contentPanel.Controls.Add(_availableNameLabel);
            _contentPanel.Controls.Add(_availableValueLabel);
            _contentPanel.Controls.Add(_descriptionLabel);
            _contentPanel.Controls.Add(_folderLabel);
            _contentPanel.Controls.Add(_folderTextBox);
            _contentPanel.Controls.Add(_browseButton);

            _contentPanel.ResumeLayout(false);
            _contentPanel.PerformLayout();

            this.Text = Resources.UpgradePHPDialogTitle;

            SetContent(_contentPanel);
            UpdateTaskForm();
        }

        protected override void OnAccept()
        {
            _folderToInstall = _folderTextBox.Text.Trim();
            _busy = true;
            _folderTextBox.Enabled = false;
            _browseButton.Enabled = false;
            UpdateTaskForm();

            StartAsyncTask(OnUpgradeDoWork, OnUpgradeCompleted);
        }

        private void OnBrowseButtonClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _folderTextBox.Text = dlg.SelectedPath;
                }
            }
        }

        private void OnCheckDoWork(object sender, DoWorkEventArgs e)
        {
            PHPBuildInfo build = _module.Proxy.GetPHPBuildInfo();
            PHPRelease update = _module.Proxy.CheckForPHPUpdate();
            bool branchSupported = (update != null) || _module.Proxy.IsPHPBranchSupported();
            e.Result = new object[] { build, update, branchSupported };
        }

        private void OnCheckCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                object[] result = (object[])e.Result;
                PHPBuildInfo build = (PHPBuildInfo)result[0];
                _update = (PHPRelease)result[1];
                bool branchSupported = (bool)result[2];

                _currentValueLabel.Text = build.DisplayName;
                if (_update != null)
                {
                    _availableValueLabel.Text = _update.DisplayName;
                    _folderTextBox.Text = GetSideBySideFolder(build.Directory, _update);
                    _folderTextBox.Enabled = true;
                    _browseButton.Enabled = true;
                }
                else if (branchSupported)
                {
                    _availableValueLabel.Text = String.Format(CultureInfo.CurrentCulture, Resources.UpgradePHPDialogUpToDate, build.Version, build.Branch);
                }
                else
                {
                    _availableValueLabel.Text = String.Format(CultureInfo.CurrentCulture, Resources.UpgradePHPDialogBranchUnsupported, build.Branch);
                }
            }
            catch (Exception ex)
            {
                _availableValueLabel.Text = String.Empty;
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }

            UpdateTaskForm();
        }

        private void OnFolderTextBoxTextChanged(object sender, EventArgs e)
        {
            UpdateTaskForm();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            StartAsyncTask(OnCheckDoWork, OnCheckCompleted);
        }

        private void OnUpgradeDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.UpgradePHP(_folderToInstall);
        }

        private void OnUpgradeCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _busy = false;
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _folderTextBox.Enabled = true;
                _browseButton.Enabled = true;
                UpdateTaskForm();
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        // Same rule as PHPInstaller.GetSideBySideDirectory on the server: next to the current PHP folder
        private static string GetSideBySideFolder(string currentDirectory, PHPRelease release)
        {
            string folderName = String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}",
                release.Version, release.ThreadSafe ? "ts" : "nts", release.Architecture);

            string parent = Path.GetDirectoryName(currentDirectory.TrimEnd('\\', '/'));
            if (String.IsNullOrEmpty(parent))
            {
                return @"%ProgramFiles%\PHP\" + folderName;
            }

            return Path.Combine(parent, folderName);
        }
    }
}

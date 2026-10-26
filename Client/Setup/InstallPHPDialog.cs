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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Web.Management.Client.Win32;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Setup
{

    /// <summary>
    /// Lists the PHP for Windows builds published at downloads.php.net and installs the selected one.
    /// </summary>
    internal sealed class InstallPHPDialog :
#if VSDesigner
        Form
#else
        TaskForm
#endif
    {
        private PHPModule _module;
        private bool _isLocalConnection;

        private ManagementPanel _contentPanel;
        private Label _versionsLabel;
        private Label _branchLabel;
        private ComboBox _branchComboBox;
        private CheckBox _showTSCheckBox;
        private CheckBox _showX86CheckBox;
        private CheckBox _showArchivesCheckBox;
        private ListView _releasesListView;
        private Label _hintLabel;
        private Label _folderLabel;
        private TextBox _folderTextBox;
        private Button _browseButton;
        private CheckBox _activateCheckBox;

        private List<PHPRelease> _releases = new List<PHPRelease>();
        private bool _archivesLoaded;
        private bool _busy;
        private bool _folderEditedByUser;
        private bool _settingFolder;

        // Values captured on the UI thread for the background installation
        private PHPRelease _releaseToInstall;
        private string _folderToInstall;
        private bool _activateToInstall;

        public InstallPHPDialog(PHPModule module, bool isLocalConnection) : base(module)
        {
            _module = module;
            _isLocalConnection = isLocalConnection;
            InitializeComponent();
            InitializeUI();
        }

        internal string InstalledHandlerName { get; private set; }

        protected override bool CanAccept
        {
            get
            {
                return !_busy && SelectedRelease != null && !String.IsNullOrEmpty(_folderTextBox.Text.Trim());
            }
        }

        protected override bool CanShowHelp
        {
            get
            {
                return false;
            }
        }

        private PHPRelease SelectedRelease
        {
            get
            {
                if (_releasesListView.SelectedItems.Count == 0)
                {
                    return null;
                }

                return (PHPRelease)_releasesListView.SelectedItems[0].Tag;
            }
        }

        private void FillBranches()
        {
            string selected = _branchComboBox.SelectedItem as string;

            _branchComboBox.BeginUpdate();
            _branchComboBox.Items.Clear();
            _branchComboBox.Items.Add(Resources.InstallPHPDialogAllBranches);
            foreach (PHPRelease release in _releases)
            {
                if (!_branchComboBox.Items.Contains(release.Branch))
                {
                    _branchComboBox.Items.Add(release.Branch);
                }
            }
            _branchComboBox.EndUpdate();

            int index = (selected != null) ? _branchComboBox.Items.IndexOf(selected) : -1;
            _branchComboBox.SelectedIndex = (index >= 0) ? index : 0;
        }

        private void FillReleases()
        {
            string branch = (_branchComboBox.SelectedIndex > 0) ? (string)_branchComboBox.SelectedItem : null;

            _releasesListView.BeginUpdate();
            _releasesListView.Items.Clear();
            foreach (PHPRelease release in _releases)
            {
                if ((branch != null && release.Branch != branch) ||
                    (release.ThreadSafe && !_showTSCheckBox.Checked) ||
                    (String.Equals(release.Architecture, "x86", StringComparison.OrdinalIgnoreCase) && !_showX86CheckBox.Checked) ||
                    (!release.IsLatest && !_showArchivesCheckBox.Checked))
                {
                    continue;
                }

                string version = release.IsLatest
                    ? String.Format(CultureInfo.CurrentCulture, Resources.InstallPHPDialogLatest, release.Version)
                    : release.Version;
                ListViewItem item = new ListViewItem(version);
                item.SubItems.Add(release.ThreadSafe ? "TS" : "NTS");
                item.SubItems.Add(release.Architecture);
                item.SubItems.Add(release.Compiler);
                item.SubItems.Add(release.FileSize);
                item.SubItems.Add(String.IsNullOrEmpty(release.Sha256) ? Resources.InstallPHPDialogChecksumNotPublished : Resources.InstallPHPDialogChecksumVerified);
                item.Tag = release;
                _releasesListView.Items.Add(item);
            }

            if (_releasesListView.Items.Count > 0)
            {
                _releasesListView.Items[0].Selected = true;
            }
            _releasesListView.EndUpdate();

            UpdateSelection();
        }

        private static string GetDefaultFolder(PHPRelease release)
        {
            return String.Format(CultureInfo.InvariantCulture, @"%ProgramFiles%\PHP\{0}-{1}-{2}",
                release.Version, release.ThreadSafe ? "ts" : "nts", release.Architecture);
        }

        private void InitializeComponent()
        {
            this._versionsLabel = new Label();
            this._branchLabel = new Label();
            this._branchComboBox = new ComboBox();
            this._showTSCheckBox = new CheckBox();
            this._showX86CheckBox = new CheckBox();
            this._showArchivesCheckBox = new CheckBox();
            this._releasesListView = new ListView();
            this._hintLabel = new Label();
            this._folderLabel = new Label();
            this._folderTextBox = new TextBox();
            this._browseButton = new Button();
            this._activateCheckBox = new CheckBox();
            this.SuspendLayout();

            this._versionsLabel.AutoSize = true;
            this._versionsLabel.Location = new System.Drawing.Point(0, 8);
            this._versionsLabel.Text = Resources.InstallPHPDialogVersions;

            this._branchLabel.AutoSize = true;
            this._branchLabel.Location = new System.Drawing.Point(0, 33);
            this._branchLabel.Text = Resources.InstallPHPDialogBranch;

            this._branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this._branchComboBox.Location = new System.Drawing.Point(60, 29);
            this._branchComboBox.Size = new System.Drawing.Size(80, 21);
            this._branchComboBox.SelectedIndexChanged += new EventHandler(OnFilterChanged);

            this._showTSCheckBox.AutoSize = true;
            this._showTSCheckBox.Location = new System.Drawing.Point(155, 31);
            this._showTSCheckBox.Text = Resources.InstallPHPDialogShowTS;
            this._showTSCheckBox.CheckedChanged += new EventHandler(OnFilterChanged);

            this._showX86CheckBox.AutoSize = true;
            this._showX86CheckBox.Location = new System.Drawing.Point(155, 54);
            this._showX86CheckBox.Text = Resources.InstallPHPDialogShowX86;
            this._showX86CheckBox.CheckedChanged += new EventHandler(OnFilterChanged);

            this._showArchivesCheckBox.AutoSize = true;
            this._showArchivesCheckBox.Location = new System.Drawing.Point(355, 31);
            this._showArchivesCheckBox.Text = Resources.InstallPHPDialogShowArchives;
            this._showArchivesCheckBox.CheckedChanged += new EventHandler(OnShowArchivesCheckedChanged);

            this._releasesListView.Location = new System.Drawing.Point(0, 78);
            this._releasesListView.Size = new System.Drawing.Size(560, 200);
            this._releasesListView.View = View.Details;
            this._releasesListView.FullRowSelect = true;
            this._releasesListView.HideSelection = false;
            this._releasesListView.MultiSelect = false;
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnVersion, 120);
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnBuild, 60);
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnArchitecture, 85);
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnCompiler, 70);
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnSize, 70);
            this._releasesListView.Columns.Add(Resources.InstallPHPDialogColumnChecksum, 130);
            this._releasesListView.SelectedIndexChanged += new EventHandler(OnReleasesSelectedIndexChanged);

            this._hintLabel.Location = new System.Drawing.Point(0, 283);
            this._hintLabel.Size = new System.Drawing.Size(560, 30);
            this._hintLabel.Text = Resources.InstallPHPDialogNTSHint;

            this._folderLabel.AutoSize = true;
            this._folderLabel.Location = new System.Drawing.Point(0, 318);
            this._folderLabel.Text = Resources.InstallPHPDialogFolder;

            this._folderTextBox.Location = new System.Drawing.Point(0, 335);
            this._folderTextBox.Size = new System.Drawing.Size(527, 20);
            this._folderTextBox.TextChanged += new EventHandler(OnFolderTextBoxTextChanged);

            this._browseButton.Location = new System.Drawing.Point(533, 333);
            this._browseButton.Size = new System.Drawing.Size(27, 23);
            this._browseButton.Text = Resources.CommonBrowseButton;
            this._browseButton.UseVisualStyleBackColor = true;
            this._browseButton.Click += new EventHandler(OnBrowseButtonClick);

            this._activateCheckBox.AutoSize = true;
            this._activateCheckBox.Checked = true;
            this._activateCheckBox.Location = new System.Drawing.Point(0, 365);
            this._activateCheckBox.Text = Resources.InstallPHPDialogActivate;

            this.ClientSize = new System.Drawing.Size(600, 470);
            this.Name = "InstallPHPDialog";
            this.ResumeLayout(false);
#if VSDesigner
            this.PerformLayout();
#endif
        }

        private void InitializeUI()
        {
            _contentPanel = new ManagementPanel();
            _contentPanel.SuspendLayout();

            // The folder is on the IIS server, so browsing is only possible on a local connection
            _browseButton.Visible = _isLocalConnection;

            _contentPanel.Location = new System.Drawing.Point(0, 0);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(_versionsLabel);
            _contentPanel.Controls.Add(_branchLabel);
            _contentPanel.Controls.Add(_branchComboBox);
            _contentPanel.Controls.Add(_showTSCheckBox);
            _contentPanel.Controls.Add(_showX86CheckBox);
            _contentPanel.Controls.Add(_showArchivesCheckBox);
            _contentPanel.Controls.Add(_releasesListView);
            _contentPanel.Controls.Add(_hintLabel);
            _contentPanel.Controls.Add(_folderLabel);
            _contentPanel.Controls.Add(_folderTextBox);
            _contentPanel.Controls.Add(_browseButton);
            _contentPanel.Controls.Add(_activateCheckBox);

            _contentPanel.ResumeLayout(false);
            _contentPanel.PerformLayout();

            this.Text = Resources.InstallPHPDialogTitle;

            SetContent(_contentPanel);
            UpdateTaskForm();
        }

        private void LoadReleases(bool includeArchives)
        {
            SetBusy(true);
            StartAsyncTask(OnGetReleasesDoWork, OnGetReleasesCompleted, null, includeArchives);
        }

        protected override void OnAccept()
        {
            _releaseToInstall = SelectedRelease;
            _folderToInstall = _folderTextBox.Text.Trim();
            _activateToInstall = _activateCheckBox.Checked;

            SetBusy(true);
            StartAsyncTask(OnInstallDoWork, OnInstallCompleted);
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

        private void OnFilterChanged(object sender, EventArgs e)
        {
            FillReleases();
        }

        private void OnFolderTextBoxTextChanged(object sender, EventArgs e)
        {
            if (!_settingFolder)
            {
                _folderEditedByUser = true;
            }
            UpdateTaskForm();
        }

        private void OnGetReleasesDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.GetAvailablePHPReleases((bool)e.Argument);
        }

        private void OnGetReleasesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                RemoteObjectCollection<PHPRelease> releases = (RemoteObjectCollection<PHPRelease>)e.Result;
                _releases = new List<PHPRelease>();
                foreach (PHPRelease release in releases)
                {
                    _releases.Add(release);
                }

                FillBranches();
                FillReleases();
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        private void OnInstallDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.InstallPHPRelease(_releaseToInstall, _folderToInstall, _activateToInstall);
        }

        private void OnInstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                InstalledHandlerName = (string)e.Result;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            LoadReleases(false);
        }

        private void OnReleasesSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSelection();
        }

        private void OnShowArchivesCheckedChanged(object sender, EventArgs e)
        {
            if (_showArchivesCheckBox.Checked && !_archivesLoaded)
            {
                _archivesLoaded = true;
                LoadReleases(true);
            }
            else
            {
                FillReleases();
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _branchComboBox.Enabled = !busy;
            _showTSCheckBox.Enabled = !busy;
            _showX86CheckBox.Enabled = !busy;
            _showArchivesCheckBox.Enabled = !busy;
            _releasesListView.Enabled = !busy;
            _folderTextBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _activateCheckBox.Enabled = !busy;
            UpdateTaskForm();
        }

        private void UpdateSelection()
        {
            PHPRelease release = SelectedRelease;
            if (release != null)
            {
                if (!_folderEditedByUser)
                {
                    _settingFolder = true;
                    _folderTextBox.Text = GetDefaultFolder(release);
                    _settingFolder = false;
                }

                _hintLabel.Text = String.IsNullOrEmpty(release.Sha256) ? Resources.InstallPHPDialogNoChecksumWarning : Resources.InstallPHPDialogNTSHint;
            }

            UpdateTaskForm();
        }
    }
}

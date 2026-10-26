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
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Web.Management.Client.Win32;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Extensions
{

    /// <summary>
    /// Searches the Windows builds of PECL extensions and installs the one compatible with the active PHP build.
    /// </summary>
    internal sealed class InstallExtensionDialog :
#if VSDesigner
        Form
#else
        TaskForm
#endif
    {
        private PHPModule _module;

        private ManagementPanel _contentPanel;
        private Label _buildLabel;
        private Label _searchLabel;
        private TextBox _searchTextBox;
        private Button _searchButton;
        private ListBox _extensionsListBox;
        private Label _versionLabel;
        private ComboBox _versionComboBox;
        private CheckBox _overwriteCheckBox;
        private Label _messageLabel;

        private bool _busy;
        private PECLPackage _packageToInstall;
        private bool _overwrite;

        public InstallExtensionDialog(PHPModule module) : base(module)
        {
            _module = module;
            InitializeComponent();
            InitializeUI();
        }

        internal string InstalledExtensionName { get; private set; }

        protected override bool CanAccept
        {
            get
            {
                return !_busy && _versionComboBox.SelectedItem != null;
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
            this._buildLabel = new Label();
            this._searchLabel = new Label();
            this._searchTextBox = new TextBox();
            this._searchButton = new Button();
            this._extensionsListBox = new ListBox();
            this._versionLabel = new Label();
            this._versionComboBox = new ComboBox();
            this._overwriteCheckBox = new CheckBox();
            this._messageLabel = new Label();
            this.SuspendLayout();

            this._buildLabel.AutoSize = true;
            this._buildLabel.Location = new System.Drawing.Point(0, 8);

            this._searchLabel.AutoSize = true;
            this._searchLabel.Location = new System.Drawing.Point(0, 33);
            this._searchLabel.Text = Resources.InstallExtensionDialogSearch;

            this._searchTextBox.Location = new System.Drawing.Point(0, 50);
            this._searchTextBox.Size = new System.Drawing.Size(340, 20);
            this._searchTextBox.KeyDown += new KeyEventHandler(OnSearchTextBoxKeyDown);

            this._searchButton.Location = new System.Drawing.Point(346, 48);
            this._searchButton.Size = new System.Drawing.Size(84, 23);
            this._searchButton.Text = Resources.InstallExtensionDialogSearchButton;
            this._searchButton.UseVisualStyleBackColor = true;
            this._searchButton.Click += new EventHandler(OnSearchButtonClick);

            this._extensionsListBox.Location = new System.Drawing.Point(0, 77);
            this._extensionsListBox.Size = new System.Drawing.Size(430, 160);
            this._extensionsListBox.SelectedIndexChanged += new EventHandler(OnExtensionsListBoxSelectedIndexChanged);

            this._versionLabel.AutoSize = true;
            this._versionLabel.Location = new System.Drawing.Point(0, 245);
            this._versionLabel.Text = Resources.InstallExtensionDialogVersion;

            this._versionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this._versionComboBox.Location = new System.Drawing.Point(0, 262);
            this._versionComboBox.Size = new System.Drawing.Size(430, 21);
            this._versionComboBox.SelectedIndexChanged += new EventHandler(OnVersionComboBoxSelectedIndexChanged);

            this._overwriteCheckBox.AutoSize = true;
            this._overwriteCheckBox.Location = new System.Drawing.Point(0, 292);
            this._overwriteCheckBox.Text = Resources.InstallExtensionDialogOverwrite;

            this._messageLabel.Location = new System.Drawing.Point(0, 318);
            this._messageLabel.Size = new System.Drawing.Size(430, 32);
            this._messageLabel.Text = Resources.InstallExtensionDialogSourceInfo;

            this.ClientSize = new System.Drawing.Size(470, 440);
            this.Name = "InstallExtensionDialog";
            this.ResumeLayout(false);
#if VSDesigner
            this.PerformLayout();
#endif
        }

        private void InitializeUI()
        {
            _contentPanel = new ManagementPanel();
            _contentPanel.SuspendLayout();

            _contentPanel.Location = new System.Drawing.Point(0, 0);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(_buildLabel);
            _contentPanel.Controls.Add(_searchLabel);
            _contentPanel.Controls.Add(_searchTextBox);
            _contentPanel.Controls.Add(_searchButton);
            _contentPanel.Controls.Add(_extensionsListBox);
            _contentPanel.Controls.Add(_versionLabel);
            _contentPanel.Controls.Add(_versionComboBox);
            _contentPanel.Controls.Add(_overwriteCheckBox);
            _contentPanel.Controls.Add(_messageLabel);

            _contentPanel.ResumeLayout(false);
            _contentPanel.PerformLayout();

            this.Text = Resources.InstallExtensionDialogTitle;

            SetContent(_contentPanel);
            UpdateTaskForm();
        }

        protected override void OnAccept()
        {
            _packageToInstall = (PECLPackage)_versionComboBox.SelectedItem;
            _overwrite = _overwriteCheckBox.Checked;

            SetBusy(true);
            StartAsyncTask(OnInstallDoWork, OnInstallCompleted);
        }

        private void OnExtensionsListBoxSelectedIndexChanged(object sender, EventArgs e)
        {
            string name = _extensionsListBox.SelectedItem as string;
            _versionComboBox.Items.Clear();
            if (String.IsNullOrEmpty(name))
            {
                UpdateTaskForm();
                return;
            }

            SetBusy(true);
            StartAsyncTask(OnGetPackagesDoWork, OnGetPackagesCompleted, null, name);
        }

        private void OnGetBuildDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.GetPHPBuildInfo();
        }

        private void OnGetBuildCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                PHPBuildInfo build = (PHPBuildInfo)e.Result;
                _buildLabel.Text = String.Format(CultureInfo.CurrentCulture, Resources.InstallExtensionDialogBuild, build.DisplayName);
                Search();
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        private void OnGetNamesDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.GetPECLExtensionNames((string)e.Argument);
        }

        private void OnGetNamesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            _extensionsListBox.BeginUpdate();
            _extensionsListBox.Items.Clear();
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                foreach (string name in (ArrayList)e.Result)
                {
                    _extensionsListBox.Items.Add(name);
                }
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
            finally
            {
                _extensionsListBox.EndUpdate();
            }
        }

        private void OnGetPackagesDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.GetCompatiblePECLPackages((string)e.Argument);
        }

        private void OnGetPackagesCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            try
            {
                if (e.Error != null)
                {
                    throw e.Error;
                }

                RemoteObjectCollection<PECLPackage> packages = (RemoteObjectCollection<PECLPackage>)e.Result;
                foreach (PECLPackage package in packages)
                {
                    _versionComboBox.Items.Add(package);
                }
                _versionComboBox.DisplayMember = "DisplayName";

                if (_versionComboBox.Items.Count > 0)
                {
                    _versionComboBox.SelectedIndex = 0;
                    _messageLabel.Text = Resources.InstallExtensionDialogSourceInfo;
                }
                else
                {
                    _messageLabel.Text = Resources.InstallExtensionDialogNoCompatible;
                }
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }

            UpdateTaskForm();
        }

        private void OnInstallDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = _module.Proxy.InstallPECLExtension(_packageToInstall, _overwrite);
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

                InstalledExtensionName = (string)e.Result;
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

            SetBusy(true);
            StartAsyncTask(OnGetBuildDoWork, OnGetBuildCompleted);
        }

        private void OnSearchButtonClick(object sender, EventArgs e)
        {
            Search();
        }

        private void OnSearchTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Search();
            }
        }

        private void OnVersionComboBoxSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTaskForm();
        }

        private void Search()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            StartAsyncTask(OnGetNamesDoWork, OnGetNamesCompleted, null, _searchTextBox.Text.Trim());
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _searchTextBox.Enabled = !busy;
            _searchButton.Enabled = !busy;
            _extensionsListBox.Enabled = !busy;
            _versionComboBox.Enabled = !busy;
            _overwriteCheckBox.Enabled = !busy;
            UpdateTaskForm();
        }
    }
}

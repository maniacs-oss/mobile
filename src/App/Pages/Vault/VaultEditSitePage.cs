﻿using System;
using System.Collections.Generic;
using System.Linq;
using Acr.UserDialogs;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Resources;
using Plugin.Connectivity.Abstractions;
using Xamarin.Forms;
using XLabs.Ioc;

namespace Bit.App.Pages
{
    public class VaultEditSitePage : ExtendedContentPage
    {
        private readonly string _siteId;
        private readonly ISiteService _siteService;
        private readonly IFolderService _folderService;
        private readonly IUserDialogs _userDialogs;
        private readonly IConnectivity _connectivity;

        public VaultEditSitePage(string siteId)
        {
            _siteId = siteId;
            _siteService = Resolver.Resolve<ISiteService>();
            _folderService = Resolver.Resolve<IFolderService>();
            _userDialogs = Resolver.Resolve<IUserDialogs>();
            _connectivity = Resolver.Resolve<IConnectivity>();

            Init();
        }

        private void Init()
        {
            var site = _siteService.GetByIdAsync(_siteId).GetAwaiter().GetResult();
            if(site == null)
            {
                // TODO: handle error. navigate back? should never happen...
                return;
            }

            var notesCell = new FormEditorCell(height: 90);
            notesCell.Editor.Text = site.Notes?.Decrypt();
            var passwordCell = new FormEntryCell(AppResources.Password, IsPassword: true, nextElement: notesCell.Editor);
            passwordCell.Entry.Text = site.Password?.Decrypt();
            var usernameCell = new FormEntryCell(AppResources.Username, nextElement: passwordCell.Entry);
            usernameCell.Entry.Text = site.Username?.Decrypt();
            usernameCell.Entry.DisableAutocapitalize = true;
            usernameCell.Entry.Autocorrect = false;

            usernameCell.Entry.FontFamily = passwordCell.Entry.FontFamily = "Courier";

            var uriCell = new FormEntryCell(AppResources.URI, Keyboard.Url, nextElement: usernameCell.Entry);
            uriCell.Entry.Text = site.Uri?.Decrypt();
            var nameCell = new FormEntryCell(AppResources.Name, nextElement: uriCell.Entry);
            nameCell.Entry.Text = site.Name?.Decrypt();

            var folderOptions = new List<string> { AppResources.FolderNone };
            var folders = _folderService.GetAllAsync().GetAwaiter().GetResult().OrderBy(f => f.Name?.Decrypt());
            int selectedIndex = 0;
            int i = 0;
            foreach(var folder in folders)
            {
                i++;
                if(folder.Id == site.FolderId)
                {
                    selectedIndex = i;
                }

                folderOptions.Add(folder.Name.Decrypt());
            }
            var folderCell = new FormPickerCell(AppResources.Folder, folderOptions.ToArray());
            folderCell.Picker.SelectedIndex = selectedIndex;

            var favoriteCell = new ExtendedSwitchCell
            {
                Text = "Favorite",
                On = site.Favorite
            };

            var deleteCell = new ExtendedTextCell { Text = AppResources.Delete, TextColor = Color.Red };
            deleteCell.Tapped += DeleteCell_Tapped;

            var table = new ExtendedTableView
            {
                Intent = TableIntent.Settings,
                EnableScrolling = true,
                HasUnevenRows = true,
                Root = new TableRoot
                {
                    new TableSection("Site Information")
                    {
                        nameCell,
                        uriCell,
                        usernameCell,
                        passwordCell,
                        folderCell
                    },
                    new TableSection
                    {
                        favoriteCell
                    },
                    new TableSection(AppResources.Notes)
                    {
                        notesCell
                    },
                    new TableSection
                    {
                        deleteCell
                    }
                }
            };

            if(Device.OS == TargetPlatform.iOS)
            {
                table.RowHeight = -1;
                table.EstimatedRowHeight = 70;
            }

            var saveToolBarItem = new ToolbarItem(AppResources.Save, null, async () =>
            {
                if(!_connectivity.IsConnected)
                {
                    AlertNoConnection();
                    return;
                }

                if(string.IsNullOrWhiteSpace(passwordCell.Entry.Text))
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired, AppResources.Password), AppResources.Ok);
                    return;
                }

                if(string.IsNullOrWhiteSpace(nameCell.Entry.Text))
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired, AppResources.Name), AppResources.Ok);
                    return;
                }

                site.Uri = uriCell.Entry.Text?.Encrypt();
                site.Name = nameCell.Entry.Text?.Encrypt();
                site.Username = usernameCell.Entry.Text?.Encrypt();
                site.Password = passwordCell.Entry.Text?.Encrypt();
                site.Notes = notesCell.Editor.Text?.Encrypt();
                site.Favorite = favoriteCell.On;

                if(folderCell.Picker.SelectedIndex > 0)
                {
                    site.FolderId = folders.ElementAt(folderCell.Picker.SelectedIndex - 1).Id;
                }
                else
                {
                    site.FolderId = null;
                }

                var saveTask = _siteService.SaveAsync(site);
                _userDialogs.ShowLoading("Saving...", MaskType.Black);
                await saveTask;

                _userDialogs.HideLoading();
                await Navigation.PopModalAsync();
                _userDialogs.SuccessToast(nameCell.Entry.Text, "Site updated.");
            }, ToolbarItemOrder.Default, 0);

            Title = "Edit Site";
            Content = table;
            ToolbarItems.Add(saveToolBarItem);
            if(Device.OS == TargetPlatform.iOS)
            {
                ToolbarItems.Add(new DismissModalToolBarItem(this, "Cancel"));
            }

            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
            }
        }

        private async void DeleteCell_Tapped(object sender, EventArgs e)
        {
            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
                return;
            }

            if(!await _userDialogs.ConfirmAsync(AppResources.DoYouReallyWantToDelete, null, AppResources.Yes, AppResources.No))
            {
                return;
            }

            var deleteTask = _siteService.DeleteAsync(_siteId);
            _userDialogs.ShowLoading("Deleting...", MaskType.Black);
            await deleteTask;
            _userDialogs.HideLoading();

            if((await deleteTask).Succeeded)
            {
                await Navigation.PopModalAsync();
                _userDialogs.SuccessToast("Site deleted.");
            }
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle, AppResources.InternetConnectionRequiredMessage, AppResources.Ok);
        }
    }
}
/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: BodyItem.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.Windows;

namespace wsl_usb_manager.Domain;

public class BodyItem(string name, Type contentType,
    PackIconKind selectedIcon, PackIconKind unselectedIcon, object? dataContext = null) : ViewModelBase
{
    private readonly Type _contentType = contentType;
    private readonly object? _dataContext = dataContext;

    private object? _content;
    private Thickness _marginRequirement = new(5);
    private string _name = name;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public PackIconKind SelectedIcon { get; set; } = selectedIcon;
    public PackIconKind UnselectedIcon { get; set; } = unselectedIcon;

    public Thickness MarginRequirement
    {
        get => _marginRequirement;
        set => SetProperty(ref _marginRequirement, value);
    }

    public object? Content => _content ??= CreateContent();
    public object? DataContext => _dataContext;
    private object? CreateContent()
    {
        var content = Activator.CreateInstance(_contentType);
        if (_dataContext != null && content is FrameworkElement element)
        {
            element.DataContext = _dataContext;
        }

        return content;
    }
}

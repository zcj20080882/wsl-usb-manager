/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: CommandImplementations.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace wsl_usb_manager.Domain;

public class CommandImplementations : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    public CommandImplementations(Action<object?> execute)
        : this(execute, null)
    { }

    public CommandImplementations(Action<object?> execute, Func<object?, bool>? canExecute)
    {
        if (execute is null) throw new ArgumentNullException(nameof(execute));

        _execute = execute;
        _canExecute = canExecute ?? (x => true);
    }

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            CommandManager.RequerySuggested += value;
        }
        remove
        {
            CommandManager.RequerySuggested -= value;
        }
    }

    public void Refresh() => CommandManager.InvalidateRequerySuggested();
}

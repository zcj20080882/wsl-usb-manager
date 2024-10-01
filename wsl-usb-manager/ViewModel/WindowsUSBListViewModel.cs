using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wsl_usb_manager.ViewModel;

public class USBDeviceViewModel : ViewModelBase
{
    private string? _busID;
    private bool _isSelected;
    private bool _isBound;
    private bool _isForced;
    private string? _name;
    private string? _description;
    private char _code;
    private double _numeric;
    private string? _food;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public char Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public double Numeric
    {
        get => _numeric;
        set => SetProperty(ref _numeric, value);
    }

    public string? Food
    {
        get => _food;
        set => SetProperty(ref _food, value);
    }
}


public class WindowsUSBListViewModel : ViewModelBase
{
    public WindowsUSBListViewModel() 
    { 
        
    }
    
}

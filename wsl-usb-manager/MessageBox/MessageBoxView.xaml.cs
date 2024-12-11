/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MessageBoxView.xaml.cs
* NameSpace: wsl_usb_manager.MessageBox
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/12/11 18:43
******************************************************************************/
using System.Windows.Documents;
using UserControl = System.Windows.Controls.UserControl;
using RichTextBox = System.Windows.Controls.RichTextBox;
using System.Text.RegularExpressions;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace wsl_usb_manager.MessageBox;

public enum MessageType
{
    Info = 0,
    Warn = 1,
    Error = 2,
};

/// <summary>
/// Interaction logic for MessageBoxView.xaml
/// </summary>
public partial class MessageBoxView : UserControl
{
    public MessageBoxView()
    {
        InitializeComponent();
    }

    public MessageBoxView(MessageType type, string caption, string message)
    {
        InitializeComponent();
        DataContext = new MessageBoxViewModule(type, caption, message);
        ShowMessage(message);
    }

    private void ShowMessage(string message)
    {
        string urlPattern = @"(http|https)://([\w-]+\.)+[\w-]+(/[\w-./?%&=]*)?";
        Regex regex = new(urlPattern);
        int lastPos = 0;
        MessageTextBox.Document.Blocks.Clear();
        Paragraph paragraph = new();
        foreach (Match match in regex.Matches(message))
        {
            // 添加普通文本
            if (match.Index > lastPos)
            {
                string text = message.Substring(lastPos, match.Index - lastPos);
                paragraph.Inlines.Add(new Run(text));
            }
            
            // 添加超链接
            string url = match.Value;
            Hyperlink hyperlink = new(new Run(url))
            {
                NavigateUri = new Uri(url),
                ToolTip = url,
                Foreground = Brushes.Blue,
                Cursor = Cursors.Hand
            };
            hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            paragraph.Inlines.Add(hyperlink);

            lastPos = match.Index + match.Length;
        }

        // 添加剩余的普通文本
        if (lastPos < message.Length)
        {
            string text = message.Substring(lastPos);
            paragraph.Inlines.Add(new Run(text));
        }

        MessageTextBox.Document.Blocks.Add(paragraph);
        //string urlPattern = @"(http|https)://([\w-]+\.)+[\w-]+(/[\w-./?%&=]*)?";
        //Regex regex = new(urlPattern);
        //int lastPos = 0;
        //MessageTextBox.Document.Blocks.Clear();
        //foreach (Match match in regex.Matches(message))
        //{
        //    // 添加普通文本
        //    if (match.Index > lastPos)
        //    {
        //        string text = message.Substring(lastPos, match.Index - lastPos);
        //        AddTextToRichTextBox(text);
        //    }

        //    // 添加超链接
        //    string url = match.Value;
        //    AddHyperlinkToRichTextBox(url);

        //    lastPos = match.Index + match.Length;
        //}

        //// 添加剩余的普通文本
        //if (lastPos < message.Length)
        //{
        //    string text = message.Substring(lastPos);
        //    AddTextToRichTextBox(text);
        //}
    }

    private void AddTextToRichTextBox(string text)
    {
        Paragraph paragraph = new();
        paragraph.Inlines.Add(new Run(text));
        MessageTextBox.Document.Blocks.Add(paragraph);
    }

    // ...

    private void AddHyperlinkToRichTextBox(string url)
    {
        Hyperlink hyperlink = new(new Run(url))
        {
            NavigateUri = new Uri(url),
            ToolTip = url,
            Foreground = Brushes.Blue,
            Cursor = Cursors.Hand // This line is correct now
        };
        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

        Paragraph paragraph = new();
        paragraph.Inlines.Add(hyperlink);
        MessageTextBox.Document.Blocks.Add(paragraph);
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}

using System.Windows;

namespace SparkForge.Views;

public partial class SystemPromptDialog : Window
{
    public string PromptText
    {
        get => PromptTextBox.Text;
        set => PromptTextBox.Text = value;
    }

    public SystemPromptDialog()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

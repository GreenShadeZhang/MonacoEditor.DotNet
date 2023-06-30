using Microsoft.UI.Xaml.Controls;

using MonacoEditorApp.ViewModels;

namespace MonacoEditorApp.Views;

public sealed partial class JsonPage : Page
{
    public JsonViewModel ViewModel
    {
        get;
    }

    public JsonPage()
    {
        ViewModel = App.GetService<JsonViewModel>();
        InitializeComponent();
    }
}

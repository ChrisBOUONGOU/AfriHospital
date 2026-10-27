using System.ComponentModel;
using AfriHospital.src.Desktop.ViewModels;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ChangingPageTitleRaisesPropertyChanged()
    {
        var viewModel = new MainWindowViewModel();
        string? changedProperty = null;
        viewModel.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        viewModel.PageTitle = "Patients";

        Assert.Equal("Patients", viewModel.PageTitle);
        Assert.Equal(nameof(MainWindowViewModel.PageTitle), changedProperty);
    }

    [Fact]
    public void SettingSameStatusDoesNotRaiseRedundantNotification()
    {
        var viewModel = new MainWindowViewModel { StatusMessage = "Prêt" };
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.StatusMessage = "Prêt";

        Assert.Equal(0, notifications);
    }
}

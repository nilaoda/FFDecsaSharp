using System.ComponentModel;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Tests.Gui;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void LanguageChangeRefreshesCachedMainWindowText()
    {
        LanguageMode originalLanguage = LocalizationService.Mode;
        try
        {
            LocalizationService.Apply(LanguageMode.English);
            var viewModel = new MainWindowViewModel();
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

            LocalizationService.Apply(LanguageMode.SimplifiedChinese);

            Assert.Equal("详情", viewModel.DetailPaneButtonText);
            Assert.Equal("就绪", viewModel.StatusText);
            Assert.Contains(nameof(MainWindowViewModel.DetailPaneButtonText), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.QueueSummary), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.StatusText), changedProperties);
        }
        finally
        {
            LocalizationService.Apply(originalLanguage);
        }
    }
}

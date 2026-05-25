using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnappyDocsConvert.App.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private Language _language = Language.English;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Language Language
    {
        get => _language;
        private set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            OnPropertyChanged();
            OnPropertyChanged("Item[]");
        }
    }

    public string this[string key] => LocalizedStrings.Get(Language, key);

    public void SetLanguage(Language language) => Language = language;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

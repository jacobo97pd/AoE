using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class ProductShell
    {
        private Text languageLabel;

        // The masthead language switch: Spanish (Spain) is the default and English the second language.
        private void LanguageSwitch()
        {
            var button = Action(safe, "Navigation Language", LanguageText(), () =>
            {
                match.Alpha.CycleLanguage();
                languageLabel.text = LanguageText();
                Navigate(page);
            });
            Place((RectTransform)button.transform, new Vector2(.395f, .896f), new Vector2(.495f, .958f));
            button.GetComponent<Image>().color = new Color(.03f, .065f, .07f, .28f);
            languageLabel = button.GetComponentInChildren<Text>();
        }

        private string LanguageText() => match.Alpha.Settings.Value.Language == 1 ? "LANGUAGE: EN" : "LANGUAGE: ES";
    }
}

namespace TorgLink.Maui.Services;

/// <summary>Logs page openings and control activations without changing ShortP2P.</summary>
internal static class UiInteractionLog
{
    private static readonly BindableProperty HookedProperty = BindableProperty.CreateAttached(
        "Hooked", typeof(bool), typeof(UiInteractionLog), false);

    private static int _appHooked;

    public static void HookApplication(Application app)
    {
        if (Interlocked.Exchange(ref _appHooked, 1) == 1)
            return;

        app.PageAppearing += (_, page) =>
        {
            if (page == null)
                return;
            AppLog.PageOpened(page.GetType().Name, page.Title);
            Attach(page);
        };
    }

    public static void Attach(Element root)
    {
        HookControl(root);
        if (root is Page page)
        {
            foreach (var item in page.ToolbarItems)
                HookToolbar(item);
        }

        if (root is not IVisualTreeElement tree)
            return;
        foreach (var child in tree.GetVisualChildren())
        {
            if (child is Element el)
                Attach(el);
        }
    }

    private static void HookControl(Element element)
    {
        if (GetHooked(element))
            return;

        switch (element)
        {
            case Button button:
                SetHooked(button);
                button.Clicked += (_, _) =>
                    AppLog.Button(button.Text ?? button.AutomationId ?? "Button", PageName(button));
                break;
            case ImageButton imageButton:
                SetHooked(imageButton);
                imageButton.Clicked += (_, _) =>
                    AppLog.Button(imageButton.AutomationId ?? "ImageButton", PageName(imageButton));
                break;
        }
    }

    private static void HookToolbar(ToolbarItem item)
    {
        if (GetHooked(item))
            return;
        SetHooked(item);
        item.Clicked += (_, _) => AppLog.Button(item.Text ?? item.AutomationId ?? "Toolbar", "Toolbar");
    }

    private static bool GetHooked(BindableObject obj) => (bool)obj.GetValue(HookedProperty);

    private static void SetHooked(BindableObject obj) => obj.SetValue(HookedProperty, true);

    private static string PageName(Element element)
    {
        for (var el = element; el != null; el = el.Parent)
        {
            if (el is Page page)
                return page.GetType().Name;
        }

        return "-";
    }
}

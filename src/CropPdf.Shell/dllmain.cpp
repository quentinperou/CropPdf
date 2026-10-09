// Crop PDF - entry of the Windows 11 File Explorer context menu ("Crop PDF" / "Recadrer le PDF").
//
// Explorer finds this COM class through the sparse package (package/AppxManifest.xml), loads the DLL
// in a surrogate process and calls IExplorerCommand. Invoke() starts CropPdf.exe, installed next to
// this DLL, once per selected file.

#include <windows.h>
#include <exdisp.h>
#include <shldisp.h>
#include <shlguid.h>
#include <shlobj_core.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <wrl/client.h>
#include <wrl/implements.h>
#include <wrl/module.h>

#include <string>

#include "resource.h"

using namespace Microsoft::WRL;

namespace
{
    HMODULE g_module = nullptr;

    // Guard against an accidental "select all" on a folder full of PDFs.
    constexpr DWORD MaxFilesPerInvoke = 16;

    // Menu label in the user's display language (string table in CropPdfMenu.rc).
    std::wstring LoadText(UINT id)
    {
        PCWSTR text = nullptr;
        int length = LoadStringW(g_module, id, reinterpret_cast<PWSTR>(&text), 0);
        return length > 0 ? std::wstring(text, length) : std::wstring(L"Crop PDF");
    }

    std::wstring AppPath()
    {
        std::wstring path(MAX_PATH, L'\0');
        for (;;)
        {
            DWORD length = GetModuleFileNameW(g_module, path.data(), static_cast<DWORD>(path.size()));
            if (length == 0)
                return {};
            if (length < path.size())
            {
                path.resize(length);
                break;
            }
            path.resize(path.size() * 2);
        }
        path.resize(path.find_last_of(L'\\') + 1);
        return path + L"CropPdf.exe";
    }

    // Asks Explorer to start the app (Raymond Chen's "launch from the desktop" technique). The app then
    // belongs to explorer.exe instead of this surrogate: otherwise it would inherit the package identity,
    // because CropPdf.exe sits in the package's external location, and Windows would show the package
    // logo (a blank tile) in the taskbar. Started by Explorer, the window may also take the foreground.
    bool LaunchThroughExplorer(const std::wstring& app, const std::wstring& arguments)
    {
        ComPtr<IShellWindows> windows;
        if (FAILED(CoCreateInstance(CLSID_ShellWindows, nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&windows))))
            return false;

        VARIANT empty{};
        long hwnd = 0;
        ComPtr<IDispatch> desktop;
        if (windows->FindWindowSW(&empty, &empty, SWC_DESKTOP, &hwnd, SWFO_NEEDDISPATCH, &desktop) != S_OK || !desktop)
            return false;

        ComPtr<IServiceProvider> provider;
        ComPtr<IShellBrowser> browser;
        ComPtr<IShellView> view;
        ComPtr<IDispatch> background;
        ComPtr<IShellFolderViewDual> folderView;
        ComPtr<IDispatch> application;
        ComPtr<IShellDispatch2> shell;
        if (FAILED(desktop.As(&provider)) ||
            FAILED(provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&browser))) ||
            FAILED(browser->QueryActiveShellView(&view)) ||
            FAILED(view->GetItemObject(SVGIO_BACKGROUND, IID_PPV_ARGS(&background))) ||
            FAILED(background.As(&folderView)) ||
            FAILED(folderView->get_Application(&application)) ||
            FAILED(application.As(&shell)))
            return false;

        BSTR file = SysAllocString(app.c_str());
        VARIANT args{};
        args.vt = VT_BSTR;
        args.bstrVal = SysAllocString(arguments.c_str());
        VARIANT directory{};
        VARIANT operation{};
        VARIANT show{};
        show.vt = VT_I4;
        show.lVal = SW_SHOWNORMAL;
        HRESULT hr = shell->ShellExecute(file, args, directory, operation, show);
        SysFreeString(file);
        VariantClear(&args);
        return SUCCEEDED(hr);
    }

    // Fallback when Explorer cannot be reached: start the app ourselves (it then keeps the package identity).
    void LaunchDirectly(const std::wstring& app, const std::wstring& arguments)
    {
        std::wstring commandLine = L"\"" + app + L"\" " + arguments;
        // The surrogate process hosting this DLL is started hidden: without an explicit show
        // command the app window would inherit that state and open minimized.
        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        startup.dwFlags = STARTF_USESHOWWINDOW;
        startup.wShowWindow = SW_SHOWNORMAL;
        PROCESS_INFORMATION process{};
        if (CreateProcessW(app.c_str(), commandLine.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process))
        {
            AllowSetForegroundWindow(process.dwProcessId);
            CloseHandle(process.hThread);
            CloseHandle(process.hProcess);
        }
    }

    void Launch(const std::wstring& app, PCWSTR file)
    {
        std::wstring arguments = L"\"" + std::wstring(file) + L"\"";
        if (!LaunchThroughExplorer(app, arguments))
            LaunchDirectly(app, arguments);
    }
}

class __declspec(uuid("5C9B0CAD-9A30-49DA-827A-1672AC5D7FB8")) CropPdfCommand final
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
public:
    // IExplorerCommand

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override
    {
        return SHStrDupW(LoadText(IDS_MENU_TITLE).c_str(), name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        std::wstring resource = AppPath() + L",0";
        return SHStrDupW(resource.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tip) override
    {
        *tip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guid) override
    {
        *guid = __uuidof(CropPdfCommand);
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    {
        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        *commands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        DWORD count = 0;
        if (!items || FAILED(items->GetCount(&count)))
            return S_OK;

        std::wstring app = AppPath();
        for (DWORD i = 0; i < count && i < MaxFilesPerInvoke; ++i)
        {
            ComPtr<IShellItem> item;
            PWSTR path = nullptr;
            if (SUCCEEDED(items->GetItemAt(i, &item)) && SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                Launch(app, path);
                CoTaskMemFree(path);
            }
        }
        return S_OK;
    }

    // IObjectWithSite

    IFACEMETHODIMP SetSite(IUnknown* site) override
    {
        m_site = site;
        return S_OK;
    }

    IFACEMETHODIMP GetSite(REFIID riid, void** site) override
    {
        if (!m_site)
        {
            *site = nullptr;
            return E_FAIL;
        }
        return m_site.CopyTo(riid, site);
    }

private:
    ComPtr<IUnknown> m_site;
};

CoCreatableClass(CropPdfCommand)

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void** instance)
{
    return Module<InProc>::GetModule().GetClassObject(clsid, riid, instance);
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

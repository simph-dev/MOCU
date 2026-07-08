using UnityEngine;
using UnityEngine.UIElements;


public class Console_DebugUiModuleHandler : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private UiReferences _uiReference;

    private TextElement _console;


    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;
        _console = _uiReference.elements.debugTab.consoleModule.console;
        CanUseUpdateMethod = true;
    }


    public void PrintToConsole(string message, bool clearTextElement = false)
    {
        if (clearTextElement) _console.text = "";
        _console.text += $"{message}\n";
    }
}

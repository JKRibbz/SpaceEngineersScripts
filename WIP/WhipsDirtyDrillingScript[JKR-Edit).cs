/*
/ //// / Whip's Quick 'n Dirty Drilling Script v1 - 9/24/2018 / //// / 

Edit by JKRibbz:
To allow the use of upward (relative to Grid) pistons 
(UNOFFICIAL NAME) Whip's Editted Quick 'n Dirty Drilling Script v2 - 20/03/2022
*/

// This should be the name of a group that contains your drills, pistons, and LCDs.
// Feel free to customize.
const string drillingSystemGroupName = "Drill System"; //Drill heads and lcd's
const string upPistonGroupName = "Pistons [Up]"; //Pistons pointing up relative to grid
const string downPistonGroupName = "Pistons [Down]"; //Pistons pointing down relative to grid

const double extendSpeed = 0.2; // meters per second
const double retractSpeed = 1.0; // meters per second

// No touchey
bool _isSetup = false;
bool _shouldDrill = false;
List<IMyTerminalBlock> _groupBlocks = new List<IMyTerminalBlock>();
List<IMyShipDrill> _drills = new List<IMyShipDrill>();
List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();
RunningSymbol _runningSymbol = new RunningSymbol();


//JKR Edit - Assigning 2 additional groups for vertical up/down pistons
List<IMyTerminalBlock> _upPistonGroup = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> _downPistonGroup = new List<IMyTerminalBlock>();
List<IMyPistonBase> _upPistons = new List<IMyPistonBase>();
List<IMyPistonBase> _downPistons = new List<IMyPistonBase>();

void Main(string argument, UpdateType updateSource)
{
    Echo(_runningSymbol.Iterate());

    #region Argument Handling
    switch (argument.ToLowerInvariant())
    {
        case "start":
            _shouldDrill = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            _isSetup = false;
            break;
        case "stop":
            _shouldDrill = false;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            break;
        case "toggle":
            _shouldDrill = !_shouldDrill;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            break;
    }
    #endregion

    // If in an update loop
    if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) != 0)
    {
        if (!_isSetup)
            _isSetup = GrabBlocks();

        // If grabbing blocks failed, stop execution here
        if (!_isSetup)
            return;

        double currentExtension;
        double maxExtension;
        double minExtension;

        CalculatePistonExtensions(out currentExtension, out maxExtension, out minExtension);

        if (_shouldDrill)
        {
            ToggleDrillPower(_drills, true);
            SetPistonVelocity(_upPistons, -extendSpeed);
            SetPistonVelocity(_downPistons, extendSpeed);            

            if (currentExtension == maxExtension)
                _shouldDrill = false;
        }
        else
        {
            ToggleDrillPower(_drills, false);
            SetPistonVelocity(_upPistons, retractSpeed);
            SetPistonVelocity(_downPistons, -retractSpeed);

            if (currentExtension == minExtension)
                Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        WriteToTextPanels(_textPanels, currentExtension, maxExtension, minExtension, _shouldDrill);
        //DisplayOrientation(_pistons, _textPanels);
    }
}

bool GrabBlocks()
{
    // Get group
    var group = GridTerminalSystem.GetBlockGroupWithName(drillingSystemGroupName);

    // Piston groups
    var upPistonGroup = GridTerminalSystem.GetBlockGroupWithName(upPistonGroupName);
    var downPistonGroup = GridTerminalSystem.GetBlockGroupWithName(downPistonGroupName);

    if (group == null)
    {
        Echo($"Error: No group with name '{drillingSystemGroupName}', Please add drill and lcd here");
        return false;
    }

    // Get group blocks
    group.GetBlocks(_groupBlocks);
    upPistonGroup.GetBlocks(_upPistonGroup);
    downPistonGroup.GetBlocks(_downPistonGroup);

    // Clear old lists
    _drills.Clear();
    _textPanels.Clear();
    _upPistons.Clear();
    _downPistons.Clear();

    // Sort through group blocks
    foreach (var block in _groupBlocks)
    {
        var drill = block as IMyShipDrill;
        if (drill != null)
        {
            _drills.Add(drill);
            continue;
        }

        var textPanel = block as IMyTextPanel;
        if (textPanel != null)
        {
            _textPanels.Add(textPanel);
            continue;
        }
    }

    foreach (var block in _upPistonGroup)
    {
        var piston = block as IMyPistonBase;
        if (piston != null)
        {
            _upPistons.Add(piston);
            continue;
        }
    }

    foreach (var block in _downPistonGroup)
    {
        var piston = block as IMyPistonBase;
        if (piston != null)
        {
            _downPistons.Add(piston);
            continue;
        }
    }

    if (_drills.Count == 0)
    {
        Echo("Error: No drills found in group");
        return false;
    }

    if (_upPistons.Count == 0 || _downPistons.Count == 0)
    {
        Echo("Error: No pistons found in group");
        return false;
    }

    if (_textPanels.Count == 0)
    {
        Echo("Info: No text panels found in group");
    }

    return true;
}

void ToggleDrillPower(List<IMyShipDrill> drills, bool toggleOn)
{
    foreach (IMyShipDrill block in drills)
    {
        block.Enabled = toggleOn;
    }
}

void SetPistonVelocity(List<IMyPistonBase> pistons, double velocity)
{
    foreach (IMyPistonBase block in pistons)
    {        
        block.Velocity = (float)velocity;
    }
}

void CalculatePistonExtensions(out double currentExtension, out double maxExtension, out double minExtension)
{
    // Sum up total piston extensions
    currentExtension = 0;
    maxExtension = 0;
    minExtension = 0;

    foreach (IMyPistonBase block in _upPistons)
    {
        currentExtension += (block.HighestPosition - block.CurrentPosition);
        maxExtension += block.HighestPosition;
        minExtension += block.LowestPosition;
    }

    foreach (IMyPistonBase block in _downPistons)
    {
        currentExtension += block.CurrentPosition;
        maxExtension += block.HighestPosition;
        minExtension += block.LowestPosition;
    }
}

void WriteToTextPanels(List<IMyTextPanel> textPanels, double currentExtension, double maxExtension, double minExtension, bool shouldDrill)
{
    string status = shouldDrill ? "Drilling..." : currentExtension == minExtension ? "Retracted" : "Retracting...";

    string progress = shouldDrill ? $"{(float)currentExtension / (maxExtension) * 100:n0}%" : $"{(float)(maxExtension - currentExtension) / (maxExtension - minExtension) * 100:n0}%";
        
    string output = $"Status: {status}\nProgress: {currentExtension:000.00} meters ({progress})\nMax: {(float)maxExtension} Current: {(float)currentExtension}";

    foreach (IMyTextPanel block in textPanels)
    {
        block.WritePublicText(output);
        if (!block.ShowText)
            block.ShowPublicTextOnScreen();
    }
}

public class RunningSymbol
{
    int _runningSymbolVariant = 0;
    int _runningSymbolCount = 0;
    int _increment = 1;
    string[] _runningSymbols = new string[] { "−", "\\", "|", "/" };

    public RunningSymbol() { }

    public RunningSymbol(int increment)
    {
        _increment = increment;
    }

    public RunningSymbol(string[] runningSymbols)
    {
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public RunningSymbol(int increment, string[] runningSymbols)
    {
        _increment = increment;
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public string Iterate(int ticks = 1)
    {
        if (_runningSymbolCount >= _increment)
        {
            _runningSymbolCount = 0;
            _runningSymbolVariant++;
            _runningSymbolVariant = _runningSymbolVariant++ % _runningSymbols.Length;
        }
        _runningSymbolCount += ticks;

        return this.ToString();
    }

    public override string ToString()
    {
        return _runningSymbols[_runningSymbolVariant];
    }
}
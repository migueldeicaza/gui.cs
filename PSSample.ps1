using namespace Terminal.Gui

# Load the Terminal.Gui assembly and its dependencies (assumed to be in the
# the same directory).
# NOTE: `using assmembly <path>` seemingly only works with full, literal paths
# as of PowerShell Core 7.1.0-preview.7.
# The assumption here is that all relevant DLLs are stored in subfolder
# assemblies/bin/Release/*/publish of the script directory, as shown in 
#   https://stackoverflow.com/a/50004706/45375
#Add-Type -Path $PSScriptRoot/assemblies/bin/Release/*/publish/Terminal.Gui.dll

# Initialize the "GUI".
# Note: This must come before creating windows and controls.
[Application]::Init()

$win = [Window] @{
  Title = 'Hello World'
}

$edt = [TextField] @{
    X = [Pos]::Center()
    Y = [Pos]::Center()
    Width = 20
    Text = 'This text will be returned'
}
$win.Add($edt)

$btn = [Button] @{
  X = [Pos]::Center()
  Y = [Pos]::Center() + 1
  Text = 'Quit'
}
$win.Add($btn)
[Application]::Top.Add($win)

# Attach an event handler to the button.
# Note: Register-ObjectEvent -Action is NOT an option, because
# the [Application]::Run() method used to display the window is blocking.
$btn.add_Clicked({
  # Close the modal window.
  # This call is also necessary to stop printing garbage in response to mouse
  # movements later.
  [Application]::RequestStop()
})

# Show the window (takes over the whole screen). 
# Note: This is a blocking call.
[Application]::Run()

# As of 1.0.0-pre.4, at least on macOS, the following two statements
# are necessary on in order for the terminal to behave properly again.
[Application]::Shutdown() # Clears the screen too; required for being able to rerun the application in the same session.

# Output something
$edt.Text.ToString()

#tput reset # To make PSReadLine work properly again, notably up- and down-arrow.

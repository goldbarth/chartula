# chartula doctor on a public repository: every check a run needs, before anything is spent.
#
# A real run, recorded with VHS (https://github.com/charmbracelet/vhs) at its own speed:
# nothing is sped up and no pause is cut, so the clip is as long as the command took.
#
# Record it from the root of this repository:
#
#   vhs docs/assets/tapes/doctor.tape
#
# Before that, once:
#   - chartula on the PATH, installed with install.sh (the clip shows the released binary)
#   - a clone of a public repository at ~/demo/ServiceDeskLite:
#       git clone https://github.com/goldbarth/ServiceDeskLite ~/demo/ServiceDeskLite
#   - ANTHROPIC_API_KEY and GITHUB_TOKEN exported in the terminal that runs vhs.
#     They are read from the environment and never written here or shown in the clip.
#
# Cost: doctor makes one model call of a few tokens and three GitHub requests.
#
# The palette is the Goldbarth ramp. A terminal theme decides what the standard
# "green" of an ok and "red" of a fail look like: green is the ramp's teal here, and the
# red, which the ramp does not have, is a muted tone chosen for this recording.

Output docs/assets/doctor.gif

Require chartula

Set Shell "bash"
Set FontFamily "JetBrainsMono Nerd Font"
Set FontSize 15
Set LetterSpacing 0
Set Width 1100
Set Height 820
Set Padding 24
Set TypingSpeed 55ms
Set Theme { "name": "Goldbarth", "background": "#12100E", "foreground": "#FAF6F0", "cursor": "#E0A34B", "selection": "#26201A", "black": "#1B1712", "red": "#E0705A", "green": "#2DD4BF", "yellow": "#E0A34B", "blue": "#14807A", "magenta": "#B07A2E", "cyan": "#2DD4BF", "white": "#9C9082", "brightBlack": "#6E655A", "brightRed": "#F08C78", "brightGreen": "#5EEAD4", "brightYellow": "#FFE6A3", "brightBlue": "#2DD4BF", "brightMagenta": "#E0A34B", "brightCyan": "#5EEAD4", "brightWhite": "#FAF6F0" }

# Truecolor, so the spinner's fade and the two colours of the summary show as designed.
Env COLORTERM "truecolor"
# The newest Sonnet, in place of the model Chartula pins by default. The clip's header
# names it, and so does the caption beside the clip.
Env Chartula__Llm__Model "claude-sonnet-5-5"

Hide
Type "cd ~/demo/ServiceDeskLite && PS1='$ ' && clear"
Enter
Sleep 500ms
Show

Sleep 800ms
Type "chartula doctor"
Sleep 600ms
Enter
# Until the prompt is back on the last line. Waiting for text on the screen stops
# working once the output scrolls.
Wait+Line@60s /^\$\s*$/
Sleep 5s

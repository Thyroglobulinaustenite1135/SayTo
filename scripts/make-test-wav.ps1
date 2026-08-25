param([string]$OutFile = "$PSScriptRoot\..\test\selftest-en.wav")

Add-Type -AssemblyName System.Speech
New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$synth.SetOutputToWaveFile($OutFile, $fmt)
$synth.Speak("Hello, this is a voice typing test for the SayTo application. The weather is nice today.")
$synth.SetOutputToDefaultAudioDevice()
$synth.Dispose()
Write-Output "WAV written: $OutFile"

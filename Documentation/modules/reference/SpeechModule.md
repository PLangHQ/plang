# SpeechModule

Hint: `[speech]`  
Type: `PLang.Modules.SpeechModule.Program`

Text to speech with Google Gemini. Turns text into a spoken audio file, Icelandic and many other languages. The api key is read from the app settings by the module, the step never carries it

## Methods

### TextToSpeech

```
TextToSpeech(String text, String filePath, String language = null, String voice = null, String style = null, String model = null, String apiKey = null) : PLang.Modules.SpeechModule.SpeechResult
```

Converts text to speech and saves it as a wav file. language is an optional hint such as is, en, da. voice is a Gemini voice name such as Kore, Puck, Charon, Aoede. style is an optional direction such as "warm and calm"


Examples:

- convert %text% to speech, language: is, save to audio.wav, write to %speech% => text=%text%, language="is", filePath="audio.wav"
- text to speech %script%, voice: Puck, save to /audio/step1.wav => text=%script%, voice="Puck", filePath="/audio/step1.wav"
- read %text% aloud, language: is, style: warm and calm, save to %path%, write to %speech% => text=%text%, language="is", style="warm and calm", filePath=%path%


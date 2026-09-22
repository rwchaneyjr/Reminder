$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$nativeDirectory = Join-Path $root 'Packages/com.whisper.unity/Plugins/Windows'
$env:PATH = $nativeDirectory + ';' + $env:PATH
$params = Get-Content (Join-Path $root 'Packages/com.whisper.unity/Runtime/Native/WhisperNativeParams.cs') -Raw
$native = (Get-Content (Join-Path $root 'Packages/com.whisper.unity/Runtime/Native/WhisperNative.cs') -Raw).Replace('using UnityEngine;', '')
$runner = @'
using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Whisper.Native;
public static unsafe class SpeechSmoke {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string path);
 public static string Run(string model, string wav) {
  byte[] audio = null;
  using(var reader = new BinaryReader(File.OpenRead(wav))) {
   reader.ReadBytes(12);
   while(reader.BaseStream.Position < reader.BaseStream.Length) {
    var tag = Encoding.ASCII.GetString(reader.ReadBytes(4)); int size = reader.ReadInt32();
    var next = reader.BaseStream.Position + size + (size % 2);
    if(tag == "fmt ") {
     if(reader.ReadInt16()!=1 || reader.ReadInt16()!=1 || reader.ReadInt32()!=16000) throw new Exception("Expected 16kHz mono PCM.");
     reader.ReadInt32(); reader.ReadInt16(); if(reader.ReadInt16()!=16) throw new Exception("Expected 16-bit PCM.");
    }
    if(tag=="data") audio = reader.ReadBytes(size);
    reader.BaseStream.Position=next;
   }
  }
  if(audio == null) throw new Exception("No WAV audio.");
  float[] samples = new float[audio.Length/2];
  for(int i=0;i<samples.Length;i++) samples[i]=BitConverter.ToInt16(audio, i*2)/32768f;
  var context = WhisperNative.whisper_context_default_params(); context.use_gpu=false;
  byte[] weights=File.ReadAllBytes(model); IntPtr ctx;
  fixed(byte* p=weights) ctx=WhisperNative.whisper_init_from_buffer_with_params((IntPtr)p,(UIntPtr)weights.Length,context);
  if(ctx==IntPtr.Zero) throw new Exception("Model load failed.");
  var language=Marshal.StringToHGlobalAnsi("en");
  try {
   var parameters=WhisperNative.whisper_full_default_params(WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY);
   parameters.n_threads=4; parameters.no_context=true; parameters.language=(byte*)language;
   fixed(float* p=samples) if(WhisperNative.whisper_full(ctx,parameters,p,samples.Length)!=0) throw new Exception("Transcription failed.");
   var result=new StringBuilder();
   for(int i=0;i<WhisperNative.whisper_full_n_segments(ctx);i++) result.Append(Marshal.PtrToStringAnsi(WhisperNative.whisper_full_get_segment_text(ctx,i)));
   return result.ToString().Trim();
  } finally { Marshal.FreeHGlobal(language); WhisperNative.whisper_free(ctx); }
 }
}
'@
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
Set-Content (Join-Path $logs 'WhisperNative.cs') $native
Set-Content (Join-Path $logs 'SpeechSmoke.cs') $runner
Add-Type -Path @((Join-Path $root 'Packages/com.whisper.unity/Runtime/Native/WhisperNativeParams.cs'), (Join-Path $logs 'WhisperNative.cs'), (Join-Path $logs 'SpeechSmoke.cs')) -CompilerOptions '/unsafe'
[SpeechSmoke]::SetDllDirectory($nativeDirectory) | Out-Null
$speaker = New-Object -ComObject SAPI.SpVoice
$stream = New-Object -ComObject SAPI.SpFileStream
$stream.Format.Type = 18
$wav = Join-Path $logs 'whisper-test.wav'
$stream.Open($wav, 3, $false)
try { $speaker.AudioOutputStream = $stream; $speaker.Speak('Call John in an hour.') | Out-Null }
finally { $stream.Close() }
$timer = [System.Diagnostics.Stopwatch]::StartNew()
$text = [SpeechSmoke]::Run((Join-Path $root 'Assets/StreamingAssets/Whisper/ggml-tiny.bin'), $wav)
$timer.Stop()
if ($text -notmatch '(?i)call.*john.*hour') { throw ('Unexpected transcript: ' + $text) }
Write-Host ('PASS: ' + $text + ' (' + $timer.Elapsed.TotalSeconds.ToString('F1') + ' seconds on Windows CPU)')

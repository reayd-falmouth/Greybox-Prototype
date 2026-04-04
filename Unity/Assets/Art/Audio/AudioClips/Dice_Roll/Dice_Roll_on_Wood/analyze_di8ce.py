import wave
import os


def analyze_dice_audio(file_path):
    if not os.path.exists(file_path):
        print(f"Error: File '{file_path}' not found.")
        return

    try:
        with wave.open(file_path, 'rb') as audio:
            # Extract header information
            params = audio.getparams()
            channels = params.nchannels
            sample_rate = params.framerate
            sample_width = params.sampwidth  # in bytes
            total_frames = params.nframes

            # Calculate actual duration based on header
            duration = total_frames / float(sample_rate)

            print(f"--- Header Analysis: {os.path.basename(file_path)} ---")
            print(f"Sample Rate:  {sample_rate} Hz")
            print(f"Channels:     {channels} ({'Mono' if channels == 1 else 'Stereo'})")
            print(f"Bit Depth:    {sample_width * 8}-bit")
            print(f"Total Frames: {total_frames}")
            print(f"Duration:     {duration:.4f} seconds")

            # HEURISTIC CHECK FOR SLOW PLAYBACK
            # Most dice hits/rolls are between 0.2s and 1.5s.
            if duration > 3.0:
                print("\n[!] ALERT: This file seems unusually long for a dice SFX.")
                print(
                    f"If this sound is meant to be a quick 'clack', it is playing at {(duration / 0.5):.1f}x slower than expected.")

            if sample_rate != 192000 and "192" in file_path:
                print("\n[!] ALERT: Filename suggests 192kHz, but header says " + str(sample_rate) + "Hz.")
                print("This is a classic 'Header Lie' that causes slow playback.")

    except Exception as e:
        print(f"Could not read WAV header: {e}")

# Change this to your actual file path to test it
import glob

# This will find every .wav file in the current folder
files = glob.glob("*.wav")

if not files:
    print("No .wav files found in this directory!")
else:
    for f in files:
        analyze_dice_audio(f)
        print("-" * 30)
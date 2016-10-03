using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using NAudio;
using NAudio.Wave;
using VideoLibrary;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Diagnostics.Contracts;

namespace DiscordBot.Utility {
	public static class AudioHelper {

		public static async Task<DownloadedVideo> DownloadYoutubeVideo(string videoID) {
			var uri = "http://www.youtube.com/watch?v=" + videoID;
			// To dispose the HttpClient
			using (var cli = Client.For(new YouTube())) {
				YouTubeVideo vid = await cli.GetVideoAsync(uri);
				DownloadedVideo d = new DownloadedVideo {
					videoFilename = Path.ChangeExtension(Path.GetTempFileName(), vid.FileExtension),
					videoTitle = vid.Title,
					videoFullName = vid.FullName,
					videoID = videoID,
					videoUri = uri,
				};

				
				File.WriteAllBytes(d.videoFilename, await vid.GetBytesAsync());
				return d;
			}
		}
		public class DownloadedVideo {
			public string videoFilename;
			public string videoTitle;
			public string videoFullName;
			public string videoUri;
			public string videoID;
		}

		public static readonly string FFMPEG_PATH = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");
		public static async Task ConvertToMp3(string inputFile, string outputMp3) {
			
			var args = string.Format("-i \"{0}\" \"{1}\"", inputFile, Path.ChangeExtension(outputMp3, ".mp3"));
			var ffmpeg = new Process {
				EnableRaisingEvents = true,
				StartInfo = {
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					FileName = FFMPEG_PATH,
					Arguments = args,
					CreateNoWindow = true,
				}
			};

			await Task.Run(() => {
				try {
					if (!ffmpeg.Start()) {
						LogHelper.LogError("Error starting FFMpeg!");
					}

					ffmpeg.StandardOutput.ReadToEnd();
					ffmpeg.WaitForExit();
					ffmpeg.Dispose();

				} catch (Exception err) {
					LogHelper.LogException("Error executing FFMpeg!", err);
				}
			});
		}

		public static byte[] ScaleVolumeSafeAllocateBuffers(byte[] audioSamples, float volume) {
			Contract.Requires(audioSamples != null);
			Contract.Requires(audioSamples.Length % 2 == 0);
			Contract.Requires(volume >= 0f && volume <= 1f);

			var output = new byte[audioSamples.Length];
			if (Math.Abs(volume - 1f) < 0.0001f) {
				Buffer.BlockCopy(audioSamples, 0, output, 0, audioSamples.Length);
				return output;
			}

			// 16-bit precision for the multiplication
			int volumeFixed = (int)Math.Round(volume * 65536d);

			for (var i = 0; i < output.Length; i += 2) {
				// The cast to short is necessary to get a sign-extending conversion
				int sample = (short)((audioSamples[i + 1] << 8) | audioSamples[i]);
				int processed = (sample * volumeFixed) >> 16;

				output[i] = (byte) processed;
				output[i + 1] = (byte) (processed >> 8);
			}

			return output;
		}

		//public unsafe static byte[] ScaleVolumeUnsafeAllocateBuffers(byte[] audioSamples, float volume) {
		//	Contract.Requires(audioSamples != null);
		//	Contract.Requires(audioSamples.Length % 2 == 0);
		//	Contract.Requires(volume >= 0f && volume <= 1f);
		//	Contract.Assert(BitConverter.IsLittleEndian);

		//	var output = new byte[audioSamples.Length];
		//	if (Math.Abs(volume - 1f) < 0.0001f) {
		//		Buffer.BlockCopy(audioSamples, 0, output, 0, audioSamples.Length);
		//		return output;
		//	}

		//	// 16-bit precision for the multiplication
		//	int volumeFixed = (int)Math.Round(volume * 65536d);

		//	int count = audioSamples.Length / 2;

		//	fixed (byte* srcBytes = audioSamples)
		//	fixed (byte* dstBytes = output) {
		//		short* src = (short*)srcBytes;
		//		short* dst = (short*)dstBytes;

		//		for (int i = count; i != 0; i--, src++, dst++)
		//			*dst = (short) (((*src) * volumeFixed) >> 16);
		//	}

		//	return output;
		//}

		public static byte[] ScaleVolumeSafeNoAlloc(byte[] audioSamples, float volume) {
			Contract.Requires(audioSamples != null);
			Contract.Requires(audioSamples.Length % 2 == 0);
			Contract.Requires(volume >= 0f && volume <= 1f);

			if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;

			// 16-bit precision for the multiplication
			int volumeFixed = (int)Math.Round(volume * 65536d);

			for (int i = 0, length = audioSamples.Length; i < length; i += 2) {
				// The cast to short is necessary to get a sign-extending conversion
				int sample = (short)((audioSamples[i + 1] << 8) | audioSamples[i]);
				int processed = (sample * volumeFixed) >> 16;

				audioSamples[i] = (byte) processed;
				audioSamples[i + 1] = (byte) (processed >> 8);
			}

			return audioSamples;
		}

		//public unsafe static byte[] ScaleVolumeUnsafeNoAlloc(byte[] audioSamples, float volume) {
		//	Contract.Requires(audioSamples != null);
		//	Contract.Requires(audioSamples.Length % 2 == 0);
		//	Contract.Requires(volume >= 0f && volume <= 1f);
		//	Contract.Assert(BitConverter.IsLittleEndian);

		//	if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;

		//	// 16-bit precision for the multiplication
		//	int volumeFixed = (int)Math.Round(volume * 65536d);

		//	int count = audioSamples.Length / 2;

		//	fixed (byte* srcBytes = audioSamples) {
		//		short* src = (short*)srcBytes;

		//		for (int i = count; i != 0; i--, src++)
		//			*src = (short) (((*src) * volumeFixed) >> 16);
		//	}

		//	return audioSamples;
		//}

		public static void SendMp3(this IAudioClient client, string filename) {
			int channelCount = client.Server.Client.GetService<AudioService>().Config.Channels;
			var outFormat = new WaveFormat(48000, 16, channelCount); // Format supported by discord.

			using (var MP3Reader = new Mp3FileReader(filename))
			using (var resampler = new MediaFoundationResampler(MP3Reader, outFormat)) {
				resampler.ResamplerQuality = 60; // Highest quality ^^
				int blockSize = outFormat.AverageBytesPerSecond / 50;
				byte[] buffer = new byte[blockSize];
				int byteCount;

				// Read audio into our buffer, and keep a loop open while data is present
				while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0) {
					// Read frame
					if (byteCount < blockSize) {
						// Incomplete frame
						for (int i = byteCount; i < blockSize; i++)
							buffer[i] = 0;
					}

					// Add to client out stream queue
					client.Send(buffer, 0, blockSize);
				}
			}
		}

	}
}

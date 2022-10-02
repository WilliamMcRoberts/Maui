﻿using AVFoundation;
using AVKit;
using CoreMedia;
using Foundation;
using UIKit;

namespace CommunityToolkit.Maui.MediaElement.PlatformView;

public class MauiMediaElement : UIView
{
	AVPlayer player;
	AVPlayerItem? playerItem;
	readonly AVPlayerViewController playerViewController;
	MediaElement? mediaElement;
	protected NSObject? playedToEndObserver;
	protected IDisposable? statusObserver;
	protected IDisposable? rateObserver;
	protected IDisposable? volumeObserver;

	public MauiMediaElement(MediaElement mediaElement)
	{
		this.mediaElement = mediaElement;

		playerViewController = new();
		
		player = new AVPlayer();
		playerViewController.Player = player;
		playerViewController.View!.Frame = Bounds;
		AddSubview(playerViewController.View);

		AddPlayedToEndObserver();
	}

	public void UpdateSource()
	{
		AVAsset? asset = null;

		if (mediaElement?.Source is UriMediaSource uriSource)
		{
			if (uriSource.Uri?.Scheme is "ms-appx")
			{
				if (uriSource.Uri.LocalPath.Length <= 1)
				{
					return;
				}

				// used for a file embedded in the application package
				asset = AVAsset.FromUrl(NSUrl.FromFilename(uriSource.Uri.LocalPath.Substring(1)));
			}
			//TODO
			//else if (uriSource.Uri?.Scheme == "ms-appdata")
			//{
			//	var filePath = ResolveMsAppDataUri(uriSource.Uri);

			//	if (string.IsNullOrEmpty(filePath))
			//		throw new ArgumentException("Invalid Uri", "Source");

			//	asset = AVAsset.FromUrl(NSUrl.FromFilename(filePath));
			//}
			else if (uriSource.Uri is not null)
			{
				var nsUrl = NSUrl.FromString(uriSource.Uri.AbsoluteUri) ??
							throw new NullReferenceException("NSUrl is null");
				asset = AVUrlAsset.Create(nsUrl);
			}
			else
			{
				throw new InvalidOperationException($"{nameof(uriSource.Uri)} is not initialized");
			}
		}
		else
		{
			if (mediaElement?.Source is FileMediaSource fileSource && fileSource.File is not null)
			{
				asset = AVAsset.FromUrl(NSUrl.FromFilename(fileSource.File));
			}
		}

		_ = asset ?? throw new NullReferenceException();

		playerItem = new AVPlayerItem(asset);
		AddStatusObserver();

		if (playerViewController.Player is not null)
		{
			playerViewController.Player.ReplaceCurrentItemWithPlayerItem(playerItem);
		}
		else
		{
			playerViewController.Player = new AVPlayer(playerItem);
			AddRateObserver();
			AddVolumeObserver();
		}

		UpdateVolume();

		if (mediaElement?.AutoPlay ?? false)
		{
			player.Play();
		}
		else
		{
			playerViewController.Player?.Pause();
			playerViewController.Player?.ReplaceCurrentItemWithPlayerItem(null);
			DestroyStatusObserver();

			if (mediaElement is not null)
			{
				mediaElement.CurrentState = MediaElementState.Stopped;
			}
		}
	}

	public void UpdateSpeed()
	{
		if (playerViewController.Player is null || mediaElement is null)
		{
			return;
		}

		playerViewController.Player.Rate = (float)mediaElement.Speed;
	}

	public void UpdateVolume()
	{
		if (playerViewController.Player is null || mediaElement is null)
		{
			return;
		}

		playerViewController.Player.Volume = (float)mediaElement.Volume;
	}

	protected void DisposeObservers(ref IDisposable? disposable)
	{
		disposable?.Dispose();
		disposable = null;
	}

	protected void DisposeObservers(ref NSObject? disposable)
	{
		disposable?.Dispose();
		disposable = null;
	}

	void AddVolumeObserver()
	{
		DestroyVolumeObserver();
		volumeObserver = playerViewController.Player?.AddObserver("volume", NSKeyValueObservingOptions.New,
			ObserveVolume);
	}

	void AddRateObserver()
	{
		DestroyRateObserver();
		rateObserver = playerViewController.Player?.AddObserver("rate", NSKeyValueObservingOptions.New,
			ObserveRate);
	}

	void AddStatusObserver()
	{
		DestroyStatusObserver();
		statusObserver = playerItem?.AddObserver("status", NSKeyValueObservingOptions.New, ObserveStatus);
	}

	void AddPlayedToEndObserver()
	{
		DestroyPlayedToEndObserver();

		playedToEndObserver =
			NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, PlayedToEnd);
	}

	void DestroyVolumeObserver() => DisposeObservers(ref volumeObserver);

	void DestroyRateObserver() => DisposeObservers(ref rateObserver);

	void DestroyStatusObserver() => DisposeObservers(ref statusObserver);

	void DestroyPlayedToEndObserver()
	{
		if (playedToEndObserver == null)
		{
			return;
		}
		NSNotificationCenter.DefaultCenter.RemoveObserver(playedToEndObserver);
		DisposeObservers(ref playedToEndObserver);
	}

	protected void ObserveStatus(NSObservedChange e)
	{
		_ = playerViewController.Player?.CurrentItem ?? throw new NullReferenceException();
		_ = mediaElement ?? throw new NullReferenceException();
		mediaElement.Volume = playerViewController.Player.Volume;

		switch (playerViewController.Player.Status)
		{
			case AVPlayerStatus.Failed:
				mediaElement.OnMediaFailed();
				break;

			case AVPlayerStatus.ReadyToPlay:
				var duration = playerViewController.Player.CurrentItem.Duration;
				if (duration.IsIndefinite)
				{
					mediaElement.Duration = TimeSpan.Zero;
				}
				else
				{
					mediaElement.Duration = TimeSpan.FromSeconds(duration.Seconds);
				}

				mediaElement.VideoHeight = (int)playerViewController.Player.CurrentItem.Asset.NaturalSize.Height;
				mediaElement.VideoWidth = (int)playerViewController.Player.CurrentItem.Asset.NaturalSize.Width;
				mediaElement.OnMediaOpened();
				mediaElement.Position = Position;
				break;
		}
	}

	protected virtual void ObserveRate(NSObservedChange e)
	{
		if (mediaElement is object)
		{
			switch (playerViewController.Player?.Rate)
			{
				case 0.0f:
					mediaElement.CurrentState = MediaElementState.Paused;
					break;

				default:
					mediaElement.CurrentState = MediaElementState.Playing;
					break;
			}

			mediaElement.Position = Position;
		}
	}

	void ObserveVolume(NSObservedChange e)
	{
		if (mediaElement is null || playerViewController?.Player is null)
		{
			return;
		}

		mediaElement.Volume = playerViewController.Player.Volume;
	}

	void PlayedToEnd(NSNotification notification)
	{
		if (mediaElement is null || notification.Object != playerViewController.Player?.CurrentItem)
		{
			return;
		}

		if (mediaElement.IsLooping)
		{
			playerViewController.Player?.Seek(CMTime.Zero);
			mediaElement.Position = Position;
			playerViewController.Player?.Play();
		}
		else
		{
			// TODO Implemeent KeepScreenOn
			//SetKeepScreenOn(false);
			mediaElement.Position = Position;

			try
			{
				Dispatcher.GetForCurrentThread()?.Dispatch(mediaElement.OnMediaEnded);
			}
			catch (Exception e)
			{
				// TODO inject ILogger everywhere and report there?
				//Log.Warning("MediaElement", $"Failed to play media to end: {e}");
			}
		}
	}

	TimeSpan Position
	{
		get
		{
			if (playerViewController.Player?.CurrentItem is null)
			{
				return TimeSpan.Zero;
			}

			var currentTime = playerViewController.Player.CurrentTime;

			if (double.IsNaN(currentTime.Seconds) || currentTime.IsIndefinite)
			{
				return TimeSpan.Zero;
			}

			return TimeSpan.FromSeconds(currentTime.Seconds);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (player is not null)
			{
				player.ReplaceCurrentItemWithPlayerItem(null);
				player.Dispose();
			}

			if (playerViewController is not null)
			{
				playerViewController.Dispose();
			}

			mediaElement = null;
		}

		base.Dispose(disposing);
	}
}
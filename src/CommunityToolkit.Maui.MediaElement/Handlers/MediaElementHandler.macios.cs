﻿using CommunityToolkit.Maui.MediaElement.PlatformView;
using Microsoft.Maui.Handlers;

namespace CommunityToolkit.Maui.MediaElement;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>
{
	protected override MauiMediaElement CreatePlatformView() => new(VirtualView);

	protected override void DisconnectHandler(MauiMediaElement platformView)
	{
		platformView.Dispose();
		base.DisconnectHandler(platformView);
	}

	public static void MapSource(MediaElementHandler handler, MediaElement mediaElement)
	{
		handler?.PlatformView.UpdateSource();
	}

	public static void MapSpeed(MediaElementHandler handler, MediaElement mediaElement)
	{
		handler?.PlatformView.UpdateSpeed();
	}

	public static void MapVolume(MediaElementHandler handler, MediaElement mediaElement)
	{
		handler?.PlatformView.UpdateVolume();
	}
}

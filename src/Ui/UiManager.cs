﻿namespace Oxide.Plugins
{
  using System;
  using System.Collections.Generic;
  using System.Drawing;
  using System.Drawing.Drawing2D;
  using System.Linq;
  using System.Reflection;
  using Oxide.Core.Configuration;
  using Oxide.Game.Rust.Cui;

  public partial class Imperium
  {
    public const string UiImageBaseUrl = "http://images.rustfactions.io.s3.amazonaws.com/";
    public const string UiMapOverlayImageUrl = "imperium://map-overlay.png";

    static class UiElement
    {
      public const string Hud = "Hud";
      public const string HudPanel = "Imperium.HudPanel";
      public const string HudPanelTop = "Imperium.HudPanel.Top";
      public const string HudPanelMiddle = "Imperium.HudPanel.Middle";
      public const string HudPanelBottom = "Imperium.HudPanel.Bottom";
      public const string HudPanelText = "Imperium.HudPanel.Text";
      public const string HudPanelIcon = "Imperium.HudPanel.Icon";
      public const string Map = "Imperium.Map";
      public const string MapCloseButton = "Imperium.Map.CloseButton";
      public const string MapBackgroundImage = "Imperium.Map.BackgroundImage";
      public const string MapClaimsImage = "Imperium.Map.ClaimsImage";
      public const string MapOverlay = "Imperium.Map.Overlay";
      public const string MapIcon = "Imperium.Map.Icon";
      public const string MapLabel = "Imperium.Map.Label";
    }

    static class UiHudIcon
    {
      public const string Badlands = UiImageBaseUrl + "icons/hud/badlands.png";
      public const string Claimed = UiImageBaseUrl + "icons/hud/claimed.png";
      public const string Clock = UiImageBaseUrl + "icons/hud/clock.png";
      public const string Defense = UiImageBaseUrl + "icons/hud/defense.png";
      public const string Harvest = UiImageBaseUrl + "icons/hud/harvest.png";
      public const string Headquarters = UiImageBaseUrl + "icons/hud/headquarters.png";
      public const string Players = UiImageBaseUrl + "icons/hud/players.png";
      public const string Sleepers = UiImageBaseUrl + "icons/hud/sleepers.png";
      public const string Taxes = UiImageBaseUrl + "icons/hud/taxes.png";
      public const string Town = UiImageBaseUrl + "icons/hud/town.png";
      public const string Warzone = UiImageBaseUrl + "icons/hud/warzone.png";
      public const string Wilderness = UiImageBaseUrl + "icons/hud/wilderness.png";
    }

    static class UiMapIcon
    {
      public const string Airfield = UiImageBaseUrl + "icons/map/airfield.png";
      public const string Cave = UiImageBaseUrl + "icons/map/cave.png";
      public const string Dome = UiImageBaseUrl + "icons/map/dome.png";
      public const string GasStation = UiImageBaseUrl + "icons/map/gas-station.png";
      public const string Harbor = UiImageBaseUrl + "icons/map/harbor.png";
      public const string Headquarters = UiImageBaseUrl + "icons/map/headquarters.png";
      public const string Junkyard = UiImageBaseUrl + "icons/map/junkyard.png";
      public const string LaunchSite = UiImageBaseUrl + "icons/map/launch-site.png";
      public const string Lighthouse = UiImageBaseUrl + "icons/map/lighthouse.png";
      public const string MilitaryTunnel = UiImageBaseUrl + "icons/map/military-tunnel.png";
      public const string MiningOutpost = UiImageBaseUrl + "icons/map/mining-outpost.png";
      public const string Player = UiImageBaseUrl + "icons/map/player.png";
      public const string PowerPlant = UiImageBaseUrl + "icons/map/power-plant.png";
      public const string Quarry = UiImageBaseUrl + "icons/map/quarry.png";
      public const string SatelliteDish = UiImageBaseUrl + "icons/map/satellite-dish.png";
      public const string SewerBranch = UiImageBaseUrl + "icons/map/sewer-branch.png";
      public const string Substation = UiImageBaseUrl + "icons/map/substation.png";
      public const string Supermarket = UiImageBaseUrl + "icons/map/supermarket.png";
      public const string Town = UiImageBaseUrl + "icons/map/town.png";
      public const string Trainyard = UiImageBaseUrl + "icons/map/trainyard.png";
      public const string Unknown = UiImageBaseUrl + "icons/map/unknown.png";
      public const string WaterTreatmentPlant = UiImageBaseUrl + "icons/map/water-treatment-plant.png";
    }

    class UiManager : ImperiumComponent
    {
      Dictionary<string, Image> Images;
      ImageDownloader Downloader;

      public UiManager(Imperium core)
        : base(core)
      {
        Images = new Dictionary<string, Image>();
        Downloader = new UnityEngine.GameObject().AddComponent<ImageDownloader>();
        Downloader.Init(core);
      }

      public Image RegisterImage(string url, byte[] imageData = null, bool overwrite = false)
      {
        Image image;

        if (Images.TryGetValue(url, out image) && !overwrite)
          return image;
        else
          image = new Image(url);

        Images[url] = image;

        if (imageData != null)
          image.Save(imageData);
        else
          Downloader.Download(image);

        return image;
      }

      public void RefreshAllImages()
      {
        foreach (Image image in Images.Values.Where(image => !image.IsGenerated))
        {
          image.Delete();
          Downloader.Download(image);
        }
      }

      public CuiRawImageComponent CreateImageComponent(string imageUrl)
      {
        Image image;

        if (!Images.TryGetValue(imageUrl, out image))
        {
          Core.PrintWarning($"Tried to create CuiRawImageComponent for unregistered image {imageUrl}. This shouldn't happen.");
          return null;
        }

        if (image.Id != null)
          return new CuiRawImageComponent { Png = image.Id, Sprite = UI_TRANSPARENT_TEXTURE };
        else
          return new CuiRawImageComponent { Url = image.Url, Sprite = UI_TRANSPARENT_TEXTURE };
      }

      public void RefreshUiForAllPlayers()
      {
        foreach (User user in Core.Users.GetAll())
        {
          user.HudPanel.Refresh();
          user.Map.Refresh();
        }
      }

      public void GenerateMapOverlayImage()
      {
        Puts("Generating new map overlay image...");

        using (var bitmap = new Bitmap(Core.Options.MapImageSize, Core.Options.MapImageSize))
        using (var graphics = Graphics.FromImage(bitmap))
        {
          var mapSize = ConVar.Server.worldsize;
          var tileSize = (int)(Core.Options.MapImageSize / (mapSize / 150f));
          var grid = new MapGrid(mapSize);

          var colorPicker = new FactionColorPicker();
          var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));

          for (int row = 0; row < grid.NumberOfCells; row++)
          {
            for (int col = 0; col < grid.NumberOfCells; col++)
            {
              Area area = Core.Areas.Get(grid.GetAreaId(row, col));
              var x = (col * tileSize);
              var y = (row * tileSize);
              var rect = new Rectangle(x, y, tileSize, tileSize);

              if (area.Type == AreaType.Badlands)
              {
                // If the tile is badlands, color it in black.
                var brush = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(32, 0, 0, 0), Color.FromArgb(255, 0, 0, 0));
                graphics.FillRectangle(brush, rect);
              }
              else if (area.Type != AreaType.Wilderness)
              {
                // If the tile is claimed, fill it with a color indicating the faction.
                var brush = new SolidBrush(colorPicker.GetColorForFaction(area.FactionId));
                graphics.FillRectangle(brush, rect);
              }
            }
          }

          var gridLabelFont = new Font("Consolas", 14, FontStyle.Bold);
          var gridLabelOffset = 5;
          var gridLinePen = new Pen(Color.FromArgb(192, 0, 0, 0), 2);

          for (int row = 0; row < grid.NumberOfCells; row++)
          {
            graphics.DrawLine(gridLinePen, 0, (row * tileSize), (grid.NumberOfCells * tileSize), (row * tileSize));
            graphics.DrawString(grid.GetRowId(row), gridLabelFont, textBrush, gridLabelOffset, (row * tileSize) + gridLabelOffset);
          }

          for (int col = 1; col < grid.NumberOfCells; col++)
          {
            graphics.DrawLine(gridLinePen, (col * tileSize), 0, (col * tileSize), (grid.NumberOfCells * tileSize));
            graphics.DrawString(grid.GetColumnId(col), gridLabelFont, textBrush, (col * tileSize) + gridLabelOffset, gridLabelOffset);
          }

          var converter = new ImageConverter();
          var imageData = (byte[]) converter.ConvertTo(bitmap, typeof(byte[]));

          Image image = RegisterImage(UiMapOverlayImageUrl, imageData, true);
          Puts($"Created new map overlay image {image.Id}.");
        }
      }

      public void Load(DynamicConfigFile file)
      {
        try
        {
          Images = file.ReadObject<Image[]>().ToDictionary(image => image.Url);
          Core.Puts($"Loaded {Images.Values.Count} cached images.");
        }
        catch (Exception err)
        {
          Core.PrintWarning($"Error loading cached images: {err}");
        }

        if (!String.IsNullOrEmpty(Core.Options.MapImageUrl))
          RegisterImage(Core.Options.MapImageUrl);

        RegisterDefaultImages(typeof(UiHudIcon));
        RegisterDefaultImages(typeof(UiMapIcon));
      }

      public void Save(DynamicConfigFile file)
      {
        file.WriteObject(Images.Values.ToArray());
      }

      public void Destroy()
      {
        UnityEngine.Object.DestroyImmediate(Downloader);
      }

      void RegisterDefaultImages(Type type)
      {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
          RegisterImage((string)field.GetRawConstantValue());
      }
    }
  }
}
 
 
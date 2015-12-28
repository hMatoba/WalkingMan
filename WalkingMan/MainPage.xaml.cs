﻿using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WalkingMan
{
    public partial class App : Application
    {
        static SQLite.SQLiteAsyncConnection db;
        static ManualResetEvent DBLoaded = new ManualResetEvent(false);

        public static Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            return Task.Run(() =>
            {
                DBLoaded.WaitOne();
                return db;
            });
        }
    }


    [Table("Album")]
    class Album
    {
        [PrimaryKey]
        public string AlbumID { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int Length { get; set; }
        public string _TrackPaths { get; private set; }
        public string _Titles { get; private set; }
        public DateTime Added { get; set; }

        [Ignore]
        public List<string> TrackPaths
        {
            get
            {
                var ja = JsonArray.Parse(_TrackPaths);
                return (from a in ja.GetArray()
                        select a.GetString()).ToList();
            }
            set
            {
                var ja = new JsonArray();
                foreach (var a in value)
                {
                    ja.Add(JsonValue.CreateStringValue(a));
                }
                _TrackPaths = ja.Stringify();
            }
        }

        [Ignore]
        public List<string> Titles
        {
            get
            {
                var ja = JsonArray.Parse(_Titles);
                return (from a in ja.GetArray()
                        select a.GetString()).ToList();
            }
            set
            {
                var ja = new JsonArray();
                foreach (var a in value)
                {
                    ja.Add(JsonValue.CreateStringValue(a));
                }
                _Titles = ja.Stringify();
            }
        }

        async public Task<Button> GetButton()
        {
            var button = new Button();
             button.Width = 140;
            button.Height = 140;
            button.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
            button.Margin = new Thickness(0);

            var s = new StackPanel();
            button.Content = s;
            var bitmapImage = new BitmapImage();
            if (TrackPaths.Count() > 0)
            {
                var thumbnail = await (await StorageFile.GetFileFromPathAsync(TrackPaths[0])).GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                bitmapImage.SetSource(thumbnail);
            }
            var image = new Image();
            image.Width = 90;
            image.Height = 90;
            image.Source = bitmapImage;
            s.Children.Add(image);
            var tBlock = new TextBlock();
            tBlock.Text = $"{this.Title}\n{this.Artist}";
            tBlock.FontSize = 12;
            tBlock.FontFamily = new Windows.UI.Xaml.Media.FontFamily("YU Gothic UI");

            s.Children.Add(tBlock);
            button.Tag = AlbumID;

            return button;
        }
    }

    public sealed partial class MainPage : Page
    {
        private Album playingAlbum = null;
        private int playingTrackNum = -1;
        private List<string> shownAlbums = new List<string>() { };
        private NotRandom notRandom;
        private List<Brush> colorPalette;

        public MainPage()
        {
            this.InitializeComponent();

            SetMembers();

            ConnectDB();

            SetEvents();

            PickAlbums();
        }

        void SetMembers()
        {
            colorPalette = new List<Brush>{
                new SolidColorBrush(Color.FromArgb(255, 95, 95, 95)),
                new SolidColorBrush(Color.FromArgb(255, 0, 60, 60)),
                new SolidColorBrush(Color.FromArgb(255, 60, 0, 60)),
                new SolidColorBrush(Color.FromArgb(255, 60, 60, 0)),
            };
            notRandom = new NotRandom(colorPalette.Count);
        }

        void ConnectDB()
        {
            var folder = ApplicationData.Current.LocalFolder.Path;
            Debug.WriteLine(folder);
            var db = new SQLiteConnection(System.IO.Path.Combine(folder, "PlaneAudioU.db"));
            db.CreateTable<Album>();
        }


        SQLiteAsyncConnection GetConnection()
        {
            return new SQLiteAsyncConnection("PlaneAudioU.db");
        }

        void SetEvents()
        {
            playButton.Click += PlayButton_Click;
            rewindButton.Click += RewindButton_Click;
            forwardButton.Click += ForwardButton_Click;

            mediaElement.MediaOpened += (sender, e) =>
            {
                seekbar.Maximum = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            };

            mediaElement.MediaEnded += async (sender, e) =>
            {
                await GoForward();
            };

            seekbar.ValueChanged += (sender, e) =>
            {
                mediaElement.Position = TimeSpan.FromSeconds(seekbar.Value);
            };

            renewButton.Click += async (sender, e) =>
            {
                renewButton.Visibility = Visibility.Collapsed;
                libProgress.Visibility = Visibility.Visible;
                libProgress.IsActive = true;
                await PickAlbumsFromDir();
                renewButton.Visibility = Visibility.Visible;
                libProgress.Visibility = Visibility.Collapsed;
                libProgress.IsActive = false;
            };

            volumeSlider.ValueChanged += (sender, e) =>
            {
                var volume = ((Slider)sender).Value;
                if (volume == 0.0)
                {
                    soundImage0.Visibility = Visibility.Visible;
                    soundImage1.Visibility = Visibility.Collapsed;
                    soundImage2.Visibility = Visibility.Collapsed;
                    soundImage3.Visibility = Visibility.Collapsed;
                    soundImage4.Visibility = Visibility.Collapsed;
                }
                else if (0.0 < volume && volume <= 0.25)
                {
                    soundImage0.Visibility = Visibility.Collapsed;
                    soundImage1.Visibility = Visibility.Visible;
                    soundImage2.Visibility = Visibility.Collapsed;
                    soundImage3.Visibility = Visibility.Collapsed;
                    soundImage4.Visibility = Visibility.Collapsed;
                }
                else if (0.25 < volume && volume <= 0.5)
                {
                    soundImage0.Visibility = Visibility.Collapsed;
                    soundImage1.Visibility = Visibility.Collapsed;
                    soundImage2.Visibility = Visibility.Visible;
                    soundImage3.Visibility = Visibility.Collapsed;
                    soundImage4.Visibility = Visibility.Collapsed;
                }
                else if (0.5 < volume && volume <= 0.75)
                {
                    soundImage0.Visibility = Visibility.Collapsed;
                    soundImage1.Visibility = Visibility.Collapsed;
                    soundImage2.Visibility = Visibility.Collapsed;
                    soundImage3.Visibility = Visibility.Visible;
                    soundImage4.Visibility = Visibility.Collapsed;
                }
                else if (0.75 < volume && volume <= 1.0)
                {
                    soundImage0.Visibility = Visibility.Collapsed;
                    soundImage1.Visibility = Visibility.Collapsed;
                    soundImage2.Visibility = Visibility.Collapsed;
                    soundImage3.Visibility = Visibility.Collapsed;
                    soundImage4.Visibility = Visibility.Visible;
                }

            };
            
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, e) =>
            {
                var t = mediaElement.Position;
                seekbar.Value = t.TotalSeconds;
            };
            timer.Start();
        }

        async void PickAlbums()
        {
            var dbConnection = new SQLiteAsyncConnection("PlaneAudioU.db");
            var albums = await dbConnection.Table<Album>().OrderBy(a => a.AlbumID).ToListAsync();
            if (albums.Count() > 0)
            {
                var preArtist = (string)null;
                foreach (var album in albums)
                {
                    if (preArtist != album.Artist)
                    {
                        albumPanel.Items.Add(GetArtistRect(album.Artist));
                        preArtist = album.Artist;
                    }
                    try
                    {
                        var button = await album.GetButton();
                        button.DoubleTapped += AlbumButton_Clicked;
                        albumPanel.Items.Add(button);
                        shownAlbums.Add(album.AlbumID);
                    }
                    catch
                    { }
                }
            }
            else
            {
                await PickAlbumsFromDir();
            }
            renewButton.Visibility = Visibility.Visible;
        }

        async Task<Album> GetAlbumFromDB(string albumID)
        {
            var dbConnection = new SQLiteAsyncConnection("PlaneAudioU.db");
            var album = await dbConnection.Table<Album>().Where(a => a.AlbumID == albumID).FirstAsync();
            return album;
        }


        async Task PickAlbumsFromDir()
        {
            var now = DateTime.Now;
            var albumFolders = await KnownFolders.MusicLibrary.GetFoldersAsync(Windows.Storage.Search.CommonFolderQuery.GroupByArtist);
            foreach (var albumFolder in albumFolders)
            {
                Debug.WriteLine(albumFolder.Name);
                var files = from b in await albumFolder.GetFilesAsync()
                            where !b.Path.EndsWith(".jpg")
                            select b;
                var propDict = (from b in files
                                select new { b.Properties.GetMusicPropertiesAsync().AsTask().Result,
                                             Value = b }
                                ).ToDictionary(b => b.Result, b => b.Value);
                var musicProperties = from b in propDict.Keys
                                      orderby b.TrackNumber
                                      group b by b.Album;
                var albumPool = new List<Album>() { };

                foreach (var tracks in musicProperties)
                {
                    var props = tracks.ToList();
                    var paths = (from b in props
                                 select propDict[b].Path).ToList();
                    var yearDesc = 10000 - props[0].Year;
                    var albumID = $"{props[0].Artist} {yearDesc} {props[0].Album}";

                    var db1 = new SQLiteAsyncConnection("PlaneAudioU.db");
                    var album = await db1
                                      .Table<Album>()
                                      .Where(a => a.AlbumID == albumID)
                                      .FirstOrDefaultAsync();
                    var albumNew = new Album()
                    {
                        AlbumID = albumID,
                        Title = props[0].Album,
                        Artist = props[0].Artist,
                        Titles = props.Select(a => a.Title).ToList(),
                        TrackPaths = paths,
                        Length = props.Count(),
                        Added = now
                    };

                    var db2 = new SQLiteAsyncConnection("PlaneAudioU.db");
                    if (album == null)
                    {

                        await db2.InsertAsync(albumNew);
                    }
                    else
                    {
                        await db2.UpdateAsync(albumNew);
                    }

                    albumPool.Add(albumNew);
                }

                Debug.WriteLine("album pool. " + albumPool.Count.ToString());
                if (albumPool.Count() > 0)
                {
                    var albumToShow = (from a in albumPool
                                       where !shownAlbums.Contains(a.AlbumID)
                                       select a).ToList();
                    if (albumToShow.Count() > 0)
                    {
                        var artistRect = GetArtistRect(albumToShow[0].Artist);
                        albumPanel.Items.Add(artistRect);
                        foreach (var album in albumToShow.OrderBy(a => a.AlbumID))
                        {
                            var button = await album.GetButton();
                            button.DoubleTapped += AlbumButton_Clicked;
                            albumPanel.Items.Add(button);
                            shownAlbums.Add(album.AlbumID);
                        }
                    }
                }

            }

            Debug.WriteLine("Saving to db.");
            var db3 = new SQLiteAsyncConnection("PlaneAudioU.db");
            foreach (var oldAlbum in await db3.Table<Album>().Where(a => now > a.Added).ToListAsync())
            {
                var db4 = new SQLiteAsyncConnection("PlaneAudioU.db");
                await db4.DeleteAsync(oldAlbum);
            }

        }

        private Border GetArtistRect(string artist)
        {
            var textBlock = new TextBlock();
            textBlock.Margin = new Thickness(0);
            textBlock.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            textBlock.Width = albumPanel.ActualWidth;
            //textBlock.Height = 138;
            textBlock.Text = artist.Replace(" ", "\n");
            textBlock.FontSize = 22;
            textBlock.FontFamily = new FontFamily("Segoe UI, Meiryo UI");
            var border = new Border();
            border.Child = textBlock;
            border.Background = colorPalette[notRandom.pop()];
            border.BorderBrush = new SolidColorBrush(Windows.UI.Colors.White);
            border.BorderThickness = new Thickness(1);
            border.Padding = new Thickness(5);
            return border;
        }

        async void AlbumButton_Clicked(object sender, RoutedEventArgs e)
        {
            var albumID = (string)(((Button)sender).Tag);
            var dbConnection = new SQLiteAsyncConnection("PlaneAudioU.db");
            playingAlbum = await dbConnection.Table<Album>().Where(a => a.AlbumID.Equals(albumID)).FirstOrDefaultAsync();
            if (playingAlbum == null)
            {
                return;
            }
            PlayTrack(await StorageFile.GetFileFromPathAsync(playingAlbum.TrackPaths[0]));
            albumTitleBlock.Text = playingAlbum.Title + "\n" + playingAlbum.Artist;
            trackPanel.Children.Clear();
            foreach (var title in playingAlbum.Titles)
            {
                var textBlock = new TextBlock();
                textBlock.FontSize = 14;
                textBlock.Text = title;
                trackPanel.Children.Add(textBlock);
            }
            playingTrackNum = 0;
            ((TextBlock)(trackPanel.Children[playingTrackNum])).FontWeight = Windows.UI.Text.FontWeights.Bold;
        }


        async void PlayTrack(StorageFile file)
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            mediaElement.SetSource(stream, "");
        }

        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.CurrentState == MediaElementState.Playing)
            {
                mediaElement.Pause();
            }
            else
            {
                mediaElement.Play();
            }
        }

        async void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            if (playingAlbum != null)
            {
                var elapsed = mediaElement.Position;
                if (TimeSpan.FromSeconds(1.5) < elapsed)
                {
                    mediaElement.Position = TimeSpan.FromSeconds(0);
                }
                else if (0 < playingTrackNum)
                {
                    ((TextBlock)(trackPanel.Children[playingTrackNum])).FontWeight = Windows.UI.Text.FontWeights.Normal;
                    playingTrackNum--;
                    ((TextBlock)(trackPanel.Children[playingTrackNum])).FontWeight = Windows.UI.Text.FontWeights.Bold;
                    PlayTrack(await StorageFile.GetFileFromPathAsync(playingAlbum.TrackPaths[playingTrackNum]));
                }
            }
        }

        async void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            await GoForward();
        }

        async Task GoForward()
        {
            if (playingAlbum != null)
            {
                if (playingTrackNum + 1 < playingAlbum.Length)
                {
                    ((TextBlock)(trackPanel.Children[playingTrackNum])).FontWeight = Windows.UI.Text.FontWeights.Normal;
                    playingTrackNum++;
                    ((TextBlock)(trackPanel.Children[playingTrackNum])).FontWeight = Windows.UI.Text.FontWeights.Bold;
                    PlayTrack(await StorageFile.GetFileFromPathAsync(playingAlbum.TrackPaths[playingTrackNum]));
                }
                else
                {
                    mediaElement.Stop();
                }
            }
        }

    }

    class NotRandom
    {
        private List<int> popped = new List<int>();
        private int maxValue;
        private Random random = new Random();

        public NotRandom(int length)
        {
            maxValue = length;
        }

        public int pop()
        {
            var num = 0;
            while (true)
            {
                num = random.Next(maxValue);
                if (!popped.Contains(num))
                {
                    break;
                }
            }
            popped.Add(num);
            if (popped.Count == maxValue)
            {
                popped = new List<int>();
            }
            return num;
        }
    }
}

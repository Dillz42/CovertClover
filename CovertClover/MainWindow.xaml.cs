using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Text.RegularExpressions;

namespace CovertClover
{
    public partial class MainWindow : Window
    {
        List<CancellationTokenSource> tokenSourceList = new List<CancellationTokenSource>();
        private int currentThread;
        public MainWindow()
        {
            InitializeComponent();

            foreach (CloverLibrary.ChanThread thread in CloverLibrary.Global.WatchFileLoad())
            {
                thread.RaiseUpdateThreadEvent += HandleUpdateThreadEvent;
                addThreadWatch(thread);
            }

            Task<List<Tuple<string, string, string>>> boardListTask = CloverLibrary.Global.GetBoardListAsync();
            boardListTask.ContinueWith(t =>
            {
                switch (t.Status)
                {
                    case TaskStatus.RanToCompletion:
                        foreach (var board in t.Result)
                        {
                            Button boardButton = new Button()
                            {
                                MinWidth = 100,
                                Margin = new Thickness(1),
                                Content = board.Item1,
                                DataContext = board,
                            };
                            boardButton.Click += BoardButton_Click;
                            BoardList.Children.Add(boardButton);

                            ToolTip toolTip = new ToolTip()
                            {
                                Content = board.Item2 + "\n" + board.Item3,
                                Placement = System.Windows.Controls.Primitives.PlacementMode.Right
                            };
                            ToolTipService.SetToolTip(boardButton, toolTip);
                        }
                        break;
                    case TaskStatus.Canceled:
                        MessageBox.Show("boardListTask was Canceled!", "CANCELED");
                        System.Diagnostics.Debugger.Break();
                        break;
                    case TaskStatus.Faulted:
                        MessageBox.Show("boardListTask was Faulted!", "FAULTED");
                        System.Diagnostics.Debugger.Break();
                        break;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        private async void BoardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                tokenSourceList.RemoveAll(ts => ts.IsCancellationRequested == true);
                foreach (CancellationTokenSource loopTokenSource in tokenSourceList)
                {
                    loopTokenSource.Cancel();
                }
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                tokenSourceList.Add(tokenSource);

                ThreadList.Children.Clear();
                ((ScrollViewer)ThreadList.Parent).ScrollToTop();

                string board = ((Tuple<string, string, string>)((Button)sender).DataContext).Item1;
                Title = board + " - " + ((Tuple<string, string, string>)((Button)sender).DataContext).Item2;

                await CloverLibrary.Global.LoadBoardAsync(board, tokenSource.Token);

                List<CloverLibrary.ChanThread> postList = CloverLibrary.Global.GetBoard(board, tokenSource.Token);
                foreach (CloverLibrary.ChanThread thread in postList)
                {
                    try
                    {
                        thread.RaiseUpdateThreadEvent += HandleUpdateThreadEvent;
                        if(thread.postDictionary.Count == 0)
                        {
                            continue;
                        }
                        UIElement threadElement = await convertPostToUIElement(thread.postDictionary.Values.First());
                        if (tokenSource.IsCancellationRequested)
                        {
                            return;
                        }
                        else
                        {
                            ThreadList.Children.Add(threadElement);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        System.Diagnostics.Debugger.Break();
                        break;
                    }
                    catch (Exception exception)
                    {
                        if (exception.Message == "404-NotFound")
                        {
                            continue;
                        }
                        //else if (exception is )
                        //{

                        //}
                        else
                        {
                            System.Diagnostics.Debugger.Break();
                            MessageBox.Show(exception.Message + "\n" + exception.StackTrace);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException && ex.Message == "Collection was modified after the enumerator was instantiated.")

                {
                    MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                    return;
                }
                else
                {
                    MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                    throw;
                }
            }
        }

        public async void ThreadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (e.OriginalSource.GetType() != typeof(Button))
                {
                    return;
                }

                tokenSourceList.RemoveAll(ts => ts.IsCancellationRequested == true);
                foreach (CancellationTokenSource loopTokenSource in tokenSourceList)
                {
                    loopTokenSource.Cancel();
                }
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                tokenSourceList.Add(tokenSource);

                ThreadList.Children.Clear();
                if (currentThread != 0)
                {
                    CloverLibrary.Global.GetThread(currentThread).MemoryClear(); 
                }
                ((ScrollViewer)ThreadList.Parent).ScrollToTop();

                CloverLibrary.ChanThread senderThread = ((CloverLibrary.ChanThread)((Button)sender).DataContext);
                Title = senderThread.board + "/" + senderThread.id + " - " + senderThread.threadName;
                ((Button)sender).Content = Regex.Replace(((Button)sender).Content.ToString(), "\\(\\d+\\) - (.*)", "$1");
                currentThread = senderThread.id;
                try
                {
                    await senderThread.LoadThreadAsync(tokenSource.Token);
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException)
                        return;
                    else if (ex.Message == "404-NotFound")
                    {
                        thread404(senderThread);
                    }
                    else
                        throw;
                }

                bool senderThreadAutoRefresh = senderThread.AutoRefresh;
                senderThread.AutoRefresh = false;
                foreach (CloverLibrary.ChanPost post in senderThread.postDictionary.Values)
                {
                    try
                    {
                        ThreadList.Children.Add(await convertPostToUIElement(post, tokenSource.Token));
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException || ex is ArgumentNullException || ex is InvalidOperationException)
                        {
                            break;
                        }
                        else
                        {
                            System.Diagnostics.Debugger.Break();
                            throw;
                        }
                    }
                }
                senderThread.AutoRefresh = senderThreadAutoRefresh;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                throw;
            }
        }

        public async void addThreadButton_Click(object sender, RoutedEventArgs e)
        {
            Regex regex = new Regex("(\\w+)/(\\d+)");
            if(regex.IsMatch(addThreadTextBox.Text))
            {
                MatchCollection matches = regex.Matches(addThreadTextBox.Text);
                string newBoard = matches[0].Groups[1].ToString();
                int no = int.Parse(matches[0].Groups[2].ToString());

                //CloverLibrary.ChanThread newThread;

                //try
                //{
                //    newThread = await CloverLibrary.Global.loadThread(newBoard, no);
                //}
                //catch (Exception ex)
                //{
                //    if (ex is TaskCanceledException)
                //        return;
                //    else if (ex.Message == "404-NotFound")
                //    {
                //        MessageBox.Show("Thread not found!");
                //        return;
                //    }
                //    else
                //        throw;
                //}
                //addThreadWatch(newThread);
                MessageBox.Show("Need to impelement this!");
            }
            else
            {
                MessageBox.Show("Not implemented!");
            }
        }

        private void setGrid(UIElement element, int column = 0, int row = 0, int colSpan = 1, int rowSpan = 1)
        {
            Grid.SetColumn(element, column);
            Grid.SetRow(element, row);
            Grid.SetColumnSpan(element, colSpan);
            Grid.SetRowSpan(element, rowSpan);
        }

        private async Task<UIElement> convertPostToUIElement(CloverLibrary.ChanPost post, 
            System.Threading.CancellationToken token = new System.Threading.CancellationToken())
        {
            Grid retVal = new Grid();
            ColumnDefinition col1 = new ColumnDefinition()
            {
                Width = GridLength.Auto
            };
            retVal.ColumnDefinitions.Add(col1);
            ColumnDefinition col2 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star)
            };
            retVal.ColumnDefinitions.Add(col2);
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());

            TextBlock textBlockSubject = new TextBlock()
            {
                FontSize = 14,
                Text = post.thread.board + "/" + post.no + ((post.sub == "") ? ("") : (" - " + post.sub)) +
                " - " + post.now + (post.resto == 0 ? " - R: " + post.replies + " / I: " + post.images : ""),
                TextWrapping = TextWrapping.Wrap
            };
            setGrid(textBlockSubject, colSpan: 2);
            retVal.Children.Add(textBlockSubject);

            if (post.ext != "")
            {
                try
                {
                    await post.LoadThumbAsync();
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    return retVal;
                }

                BitmapImage source = new BitmapImage();
                source.BeginInit();
                source.StreamSource = new System.IO.MemoryStream(await post.GetThumbDataAsync());
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.EndInit();
                source.Freeze();

                Image img = new Image();
                img.Source = source;
                img.MaxWidth = post.tn_w;
                img.HorizontalAlignment = HorizontalAlignment.Left;
                img.VerticalAlignment = VerticalAlignment.Top;

                if (post.replyList.Count > 0)
                {
                    TextBlock replyTextBlock = new TextBlock()
                    {
                        Foreground = Brushes.Blue,
                        TextWrapping = TextWrapping.Wrap
                    };
                    foreach (int replyFrom in post.replyList)
                    {
                        replyTextBlock.Text += ">" + replyFrom + "  ";
                    }
                    setGrid(replyTextBlock, row: 1, colSpan: 2);
                    retVal.Children.Add(replyTextBlock);
                }

                ToolTip imageToolTip = new ToolTip();
                StackPanel toolTipStackPanel = new StackPanel();
                TextBlock toolTipTextBlock = new TextBlock();
                toolTipStackPanel.Orientation = Orientation.Vertical;

                toolTipTextBlock.Text = post.tim + "-" + post.filename + post.ext + " - " + post.w + " x " + post.h;
                toolTipStackPanel.Children.Add(toolTipTextBlock);

                imageToolTip.Loaded += async (ls, le) =>
                {
                    if (post.ext == ".gif" || post.ext == ".webm")
                    {
                        MediaElement toolTipImage = new MediaElement();
                        try
                        {
                            if (post.imageSaved)
                            {
                                toolTipImage.Source = new Uri(post.ImagePath);
                                CloverLibrary.Global.Log("Loading animated from file");
                            }
                            else
                            {
                                toolTipImage.Source = new Uri(
                                    CloverLibrary.Global.BASE_IMAGE_URL + post.thread.board + "/" + post.tim + post.ext);
                                CloverLibrary.Global.Log("Loading animated from web");
                            }
                            toolTipImage.Volume = 1;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
                            throw;
                        }
                        double workingHeight = SystemParameters.WorkArea.Height * .85;
                        toolTipImage.MaxWidth = post.w > SystemParameters.WorkArea.Width ? SystemParameters.WorkArea.Width : post.w;
                        toolTipImage.MaxHeight = post.h > workingHeight ? workingHeight : post.h;

                        toolTipStackPanel.Children.Add(toolTipImage);
                    }
                    else
                    {
                        Image toolTipImage = new Image();
                        BitmapImage imageToolTipSource = new BitmapImage();
                        await post.LoadImageAsync(token);
                        imageToolTipSource.BeginInit();
                        imageToolTipSource.StreamSource = new System.IO.MemoryStream(await post.GetImageDataAsync());
                        imageToolTipSource.CacheOption = BitmapCacheOption.OnLoad;
                        imageToolTipSource.EndInit();
                        imageToolTipSource.Freeze();
                        toolTipImage.Source = imageToolTipSource;

                        double workingHeight = SystemParameters.WorkArea.Height * .85;
                        toolTipImage.MaxWidth = post.w > SystemParameters.WorkArea.Width ? SystemParameters.WorkArea.Width : post.w;
                        toolTipImage.MaxHeight = post.h > workingHeight ? workingHeight : post.h;

                        toolTipStackPanel.Children.Add(toolTipImage);
                    }
                };
                imageToolTip.Unloaded += (uls, ule) =>
                {
                    CloverLibrary.Global.Log(post, "Unloading from closing tooltip");
                    try
                    {
                        toolTipStackPanel.Children.Remove(toolTipStackPanel.Children.OfType<Image>().First());
                    } catch (InvalidOperationException ex) {if (ex.Message != "Sequence contains no elements"){throw;}}
                    try
                    {
                        toolTipStackPanel.Children.Remove(toolTipStackPanel.Children.OfType<MediaElement>().First());
                    }
                    catch (InvalidOperationException ex) { if (ex.Message != "Sequence contains no elements") { throw; } }

                    post.ClearImageData();
                };
                imageToolTip.Content = toolTipStackPanel;
                ToolTipService.SetShowDuration(img, int.MaxValue);
                ToolTipService.SetInitialShowDelay(img, 0);
                ToolTipService.SetToolTip(img, imageToolTip);
                setGrid(img, row: 2);
                retVal.Children.Add(img); 
            }

            TextBlock textBlockComment = new TextBlock()
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            char[] delim = { '\n' };
            string[] lines = post.com.Split(delim);
            foreach (string line in lines)
            {
                if (line.StartsWith(">>"))
                {
                    Hyperlink hyperlink = new Hyperlink() { Foreground = Brushes.Blue, NavigateUri = new Uri("http://example.com") };
                    hyperlink.Inlines.Add(line + "\n");
                    hyperlink.RequestNavigate += (s, e) =>
                    {

                    };
                    textBlockComment.Inlines.Add(hyperlink);
                }
                else
                {
                    Regex regex = new Regex("(.*)<(\\w+).*?class=\"(\\w+)\">(.*?)</\\w+>(.*)");
                    switch (regex.Replace(line, "$3"))
                    {
                        case "quote":
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$1")));
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$4"))
                                { Foreground = Brushes.ForestGreen });
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$5")));
                            break;
                        case "deadlink":
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$1")));
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$4"))
                                { Foreground = Brushes.Blue, TextDecorations = TextDecorations.Strikethrough });
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$5")));
                            break;
                        case "quotelink":
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$1")));
                            Hyperlink hyperlink = new Hyperlink() { Foreground = Brushes.Blue, NavigateUri = new Uri("http://example.com") };
                            hyperlink.Inlines.Add(regex.Replace(line, "$4"));
                            hyperlink.RequestNavigate += (s, e) =>
                            {

                            };
                            textBlockComment.Inlines.Add(hyperlink);
                            textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$5")));
                            break;
                        default:
                            textBlockComment.Inlines.Add(new Run(line));
                            break;
                    }
                    textBlockComment.Inlines.Add(new Run("\n"));
                }
                
                //else if (Regex.Match(line, "<span class=\"(\\w+)\">(.*?)</span>").Success)
                //{
                //    Regex regex = new Regex("<span class=\"(\\w+)\">(.*?)</span>");
                //    switch (Regex.Replace(line, "<span class=\"(\\w+)\">(.*?)</span>", "$1"))
                //    {
                //        default:
                //            break;
                //    }
                //    textBlockComment.Inlines.Add(new Run(regex.Replace(line, "$2") + "\n") { Foreground = Brushes.ForestGreen });
                //}
                //else
                //{
                //    textBlockComment.Inlines.Add(new Run(line + "\n"));
                //}
            }
            setGrid(textBlockComment, column: 1, row: 2);
            retVal.Children.Add(textBlockComment);
            
            if (post.resto == 0)
            {
                Button button = new Button()
                {
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = retVal,
                    Margin = new Thickness(2)
                };
                button.Click += (cs, ce) =>
                {
                    addThreadWatch(post.thread);
                };

                return button; 
            }
            else
            {
                Separator seperator = new Separator()
                {
                    Foreground = Brushes.Blue,
                    Background = Brushes.Green,
                    BorderBrush = Brushes.Pink
                };
                setGrid(seperator, row: 3, colSpan: 2);
                retVal.Children.Add(seperator);

                return retVal;
            }
        }

        private void addThreadWatch(CloverLibrary.ChanThread thread)
        {
            Button title = new Button()
            {
                Content = thread.board + "/" + thread.id + " - " + thread.threadName,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FlowDirection = FlowDirection.LeftToRight,
                Background = Brushes.DarkGreen,
                Foreground = Brushes.DarkGreen,
                Margin = new Thickness(3)
            };
            title.Click += ThreadButton_Click;

            ToolTip titleToolTip = new ToolTip()
            {
                Content = title.Content
            };
            ToolTipService.SetToolTip(title, titleToolTip);

            StackPanel expanderStackPanel = new StackPanel()
            {
                FlowDirection = FlowDirection.LeftToRight,
                Orientation = Orientation.Vertical
            };

            Expander expander = new Expander()
            {
                DataContext = thread,
                Content = expanderStackPanel,
                FlowDirection = FlowDirection.RightToLeft,
                Header = title,
                Margin = new Thickness(2)
            };

            CheckBox autoSave = new CheckBox()
            {
                Content = "Auto-save images",
                IsChecked = thread.SaveImages
            };
            autoSave.Unchecked += (s, e) => { thread.SaveImages = false; };
            autoSave.Checked += (s, e) => { thread.SaveImages = true; };

            CheckBox autoReload = new CheckBox()
            {
                Content = "AutoReload",
                IsChecked = thread.AutoRefresh
            };
            autoReload.Unchecked += (s, e) => { autoSave.IsEnabled = false; autoSave.IsChecked = false; thread.AutoRefresh = false; };
            autoReload.Checked += (s, e) => { autoSave.IsEnabled = true; thread.AutoRefresh = true; };

            expanderStackPanel.Children.Add(autoReload);
            expanderStackPanel.Children.Add(autoSave);

            Grid webFileGrid = new Grid();
            webFileGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            webFileGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            expanderStackPanel.Children.Add(webFileGrid);

            Button webButton = new Button()
            {
                Content = "Web",
                Margin = new Thickness(3)
            };
            webButton.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start("http://boards.4chan.org/" + thread.board + "/thread/" + thread.id);
            };
            Grid.SetColumn(webButton, 0);
            webFileGrid.Children.Add(webButton);

            Button fileButton = new Button()
            {
                Content = "Folder",
                Margin = new Thickness(3)
            };
            fileButton.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start(thread.GetDir());
            };
            Grid.SetColumn(fileButton, 1);
            webFileGrid.Children.Add(fileButton);

            Button removeButton = new Button()
            {
                Content = "Remove",
                Foreground = Brushes.DarkRed,
                Margin = new Thickness(3)
            };
            removeButton.Click += (s, e) => 
            {
                ThreadWatchList.Children.Remove(expander);
                thread.AutoRefresh = false;
                thread.autoRefreshThread.Abort();
                CloverLibrary.Global.WatchFileRemove(thread);
                e.Handled = true;
            };
            expanderStackPanel.Children.Add(removeButton);

            ThreadWatchList.Children.Add(expander);
            CloverLibrary.Global.WatchFileAdd(thread);
        }

        public void HandleUpdateThreadEvent(object sender, CloverLibrary.UpdateThreadEventArgs args)
        {
            CloverLibrary.ChanThread senderThread = (CloverLibrary.ChanThread)sender;

            switch (args.Update_Event)
            {
                case CloverLibrary.UpdateThreadEventArgs.UpdateEvent.unknown:
                    MessageBox.Show("Unknown event!");
                    if(args.Context is Exception)
                    {
                        MessageBox.Show(((Exception)args.Context).Message + "\n" + ((Exception)args.Context).StackTrace);
                    }
                    System.Diagnostics.Debugger.Break();
                    break;
                case CloverLibrary.UpdateThreadEventArgs.UpdateEvent.thread404:
                    thread404(senderThread);
                    break;
                case CloverLibrary.UpdateThreadEventArgs.UpdateEvent.newPosts:
                    if (currentThread == senderThread.id)
                    {
                        foreach (CloverLibrary.ChanPost post in args.postList)
                        {
                            ThreadList.Dispatcher.BeginInvoke((Action)(async () =>
                            {
                                ThreadList.Children.Add(await convertPostToUIElement(post));
                            }));
                        } 
                    }
                    else
                    {
                        ThreadWatchList.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            Expander threadWatchExpander = ThreadWatchList.Children.OfType<Expander>().Where(sp => sp.DataContext == senderThread).First();
                            Button threadButton = (Button)threadWatchExpander.Header;
                            threadButton.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                int currentNewPostCount = 0;
                                Match match = Regex.Match(threadButton.Content.ToString(), "(?:\\((\\d+)\\) - )");
                                if (match.Success)
                                {
                                    currentNewPostCount = int.Parse(match.Groups[1].ToString());
                                }
                                threadButton.Content = Regex.Replace(threadButton.Content.ToString(), 
                                    "(?:\\(\\d+\\) - )?(\\w+/\\d+)", 
                                    "(" + (args.postList.Count + currentNewPostCount) + ") - $1");
                            }));
                        }));
                    }
                    break;
                default:
                    break;
            }
        }

        public void thread404(CloverLibrary.ChanThread thread)
        {
            ThreadWatchList.Dispatcher.BeginInvoke((Action)(() =>
            {
                Expander threadWatchExpander = ThreadWatchList.Children.OfType<Expander>().Where(sp => sp.DataContext == thread).First();
                ((Button)threadWatchExpander.Header).Foreground = Brushes.Firebrick;
                ((Button)threadWatchExpander.Header).Content = 
                    Regex.Replace(((Button)threadWatchExpander.Header).Content.ToString(),
                    "(\\w+/\\d+ -)(.*)", "$1 404 -$2");
                foreach (CheckBox checkBox in ((StackPanel)threadWatchExpander.Content).Children.OfType<CheckBox>())
                {
                    checkBox.IsChecked = false;
                    checkBox.IsEnabled = false;
                }

            }));
            thread.On404();
        }
    }
}

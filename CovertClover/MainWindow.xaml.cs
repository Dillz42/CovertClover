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

            Task<List<CloverLibrary.ChanThread>> watchFileLoad = CloverLibrary.Global.watchFileLoad();
            watchFileLoad.ContinueWith(t =>
            {
                switch (t.Status)
                {
                    case TaskStatus.RanToCompletion:
                        foreach (CloverLibrary.ChanThread thread in t.Result)
                        {
                            thread.raiseUpdateThreadEvent += HandleUpdateThreadEvent;
                            addThreadWatch(thread);
                        }
                        break;
                    case TaskStatus.Canceled:
                        break;
                    case TaskStatus.Faulted:
                        break;
                    default:
                        break;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            Task<List<Tuple<string, string, string>>> boardListTask = CloverLibrary.Global.getBoardList();
            boardListTask.ContinueWith(t =>
            {
                switch (t.Status)
                {
                    case TaskStatus.RanToCompletion:
                        foreach (var board in t.Result)
                        {
                            Button boardButton = new Button();
                            boardButton.MinWidth = 100;
                            boardButton.Margin = new Thickness(1);
                            boardButton.Content = board.Item1;
                            boardButton.Click += BoardButton_Click;
                            boardButton.DataContext = board;
                            BoardList.Children.Add(boardButton);

                            ToolTip toolTip = new ToolTip();
                            toolTip.Content = board.Item2 + "\n" + board.Item3;
                            toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
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

                await CloverLibrary.Global.loadBoard(board, tokenSource.Token);

                List<CloverLibrary.ChanThread> postList = CloverLibrary.Global.getBoard(board, tokenSource.Token);
                foreach (CloverLibrary.ChanThread thread in postList)
                {
                    try
                    {
                        thread.raiseUpdateThreadEvent += HandleUpdateThreadEvent;
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
                ((ScrollViewer)ThreadList.Parent).ScrollToTop();

                CloverLibrary.ChanThread senderThread = ((CloverLibrary.ChanThread)((Button)sender).DataContext);
                Title = Regex.Replace(Title, "(\\w+ - .*?) : .*", "$1 : " + senderThread.threadName);
                ((Button)sender).Content = Regex.Replace(((Button)sender).Content.ToString(), "\\(\\d+\\) - (.*)", "$1");
                currentThread = senderThread.id;
                try
                {
                    await CloverLibrary.Global.loadThread(senderThread, tokenSource.Token);
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

                bool senderThreadAutoRefresh = senderThread.autoRefresh;
                senderThread.autoRefresh = false;
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
                senderThread.autoRefresh = senderThreadAutoRefresh;
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

                CloverLibrary.ChanThread newThread;

                try
                {
                    newThread = await CloverLibrary.Global.loadThread(newBoard, no);
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException)
                        return;
                    else if (ex.Message == "404-NotFound")
                    {
                        MessageBox.Show("Thread not found!");
                        return;
                    }
                    else
                        throw;
                }
                addThreadWatch(newThread);
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
            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = GridLength.Auto;
            retVal.ColumnDefinitions.Add(col1);
            ColumnDefinition col2 = new ColumnDefinition();
            col2.Width = new GridLength(1, GridUnitType.Star);
            retVal.ColumnDefinitions.Add(col2);
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());
            retVal.RowDefinitions.Add(new RowDefinition());

            TextBlock textBlockSubject = new TextBlock();
            textBlockSubject.FontSize = 14;
            textBlockSubject.Text = post.thread.board + "/" + post.no + ((post.sub == "") ? ("") : (" - " + post.sub)) +
                " - " + post.now + (post.resto == 0 ? " - R: " + post.replies + " / I: " + post.images : "");
            textBlockSubject.TextWrapping = TextWrapping.Wrap;
            setGrid(textBlockSubject, colSpan: 2);
            retVal.Children.Add(textBlockSubject);

            try
            {
                await post.loadThumb();
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                return retVal;
            }

            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.StreamSource = new System.IO.MemoryStream(post.thumbData);
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();
            source.Freeze();

            Image img = new Image();
            img.Source = source;
            img.MaxWidth = post.tn_w;
            img.HorizontalAlignment = HorizontalAlignment.Left;
            img.VerticalAlignment = VerticalAlignment.Top;

            if(post.replyList.Count > 0)
            {
                TextBlock replyTextBlock = new TextBlock();
                replyTextBlock.Foreground = Brushes.Blue;
                replyTextBlock.TextWrapping = TextWrapping.Wrap;
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
            FrameworkElement toolTipImage;
            if (post.ext == ".gif" || post.ext == ".webm")
            {
                toolTipImage = new MediaElement();
            } else {
                toolTipImage = new Image();
            }

            toolTipStackPanel.Orientation = Orientation.Vertical;

            toolTipTextBlock.Text = post.tim + "-" + post.filename + post.ext + " - " + post.w + " x " + post.h;
            toolTipStackPanel.Children.Add(toolTipTextBlock);

            imageToolTip.Loaded += async (ls, le) =>
            {
                await post.loadImage();
                BitmapImage imageToolTipSource = new BitmapImage();
                if (post.ext == ".gif" || post.ext == ".webm")
                {
                    try
                    {
                        ((MediaElement)toolTipImage).Source = new Uri(
                        CloverLibrary.Global.BASE_IMAGE_URL + post.thread.board + "/" + post.tim + post.ext);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
                        throw;
                    }
                }
                else
                {
                    imageToolTipSource.BeginInit();
                    imageToolTipSource.StreamSource = new System.IO.MemoryStream(post.imageData);
                    imageToolTipSource.CacheOption = BitmapCacheOption.OnLoad;
                    imageToolTipSource.EndInit();
                    imageToolTipSource.Freeze();
                    ((Image)toolTipImage).Source = imageToolTipSource;
                }
                double workingHeight = SystemParameters.WorkArea.Height * .85;
                toolTipImage.MaxWidth = post.w > SystemParameters.WorkArea.Width ? SystemParameters.WorkArea.Width : post.w;
                toolTipImage.MaxHeight = post.h > workingHeight ? workingHeight : post.h;
            };
            toolTipStackPanel.Children.Add(toolTipImage);
            imageToolTip.Content = toolTipStackPanel;
            ToolTipService.SetShowDuration(img, int.MaxValue);
            ToolTipService.SetInitialShowDelay(img, 0);
            ToolTipService.SetToolTip(img, imageToolTip);
            setGrid(img, row: 2);
            retVal.Children.Add(img);

            TextBlock textBlockComment = new TextBlock();
            textBlockComment.FontSize = 14;
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
                textBlockComment.TextWrapping = TextWrapping.Wrap;
            setGrid(textBlockComment, column: 1, row: 2);
            retVal.Children.Add(textBlockComment);
            
            if (post.resto == 0)
            {
                Button button = new Button();

                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Content = retVal;
                button.Margin = new Thickness(2);
                button.Click += (cs, ce) =>
                {
                    addThreadWatch(post.thread);
                };

                return button; 
            }
            else
            {
                Separator seperator = new Separator();
                seperator.Foreground = Brushes.Blue;
                seperator.Background = Brushes.Green;
                seperator.BorderBrush = Brushes.Pink;
                setGrid(seperator, row: 3, colSpan: 2);
                retVal.Children.Add(seperator);

                return retVal;
            }
        }

        private void addThreadWatch(CloverLibrary.ChanThread thread)
        {
            //Button threadButton = new Button();
            //StackPanel threadStackPanel = new StackPanel();
            Button title = new Button();
            Expander expander = new Expander();
            StackPanel expanderStackPanel = new StackPanel();
            CheckBox autoReload = new CheckBox();
            CheckBox autoSave = new CheckBox();
            Button webButton = new Button();
            Button removeButton = new Button();
            
            title.Content = thread.board + "/" + thread.id+ " - " + thread.threadName;
            title.HorizontalContentAlignment = HorizontalAlignment.Left;
            title.FlowDirection = FlowDirection.LeftToRight;
            title.Background = Brushes.DarkGreen;
            title.Foreground = Brushes.DarkGreen;
            title.Click += ThreadButton_Click;
            ToolTip titleToolTip = new ToolTip();
            titleToolTip.Content = title.Content;
            title.Margin = new Thickness(3);
            ToolTipService.SetToolTip(title, titleToolTip);

            expanderStackPanel.FlowDirection = FlowDirection.LeftToRight;

            expander.DataContext = thread;
            expander.Content = expanderStackPanel;
            expander.FlowDirection = FlowDirection.RightToLeft;
            expander.Header = title;
            expander.Margin = new Thickness(2);
            expanderStackPanel.Orientation = Orientation.Vertical;

            autoReload.Content = "AutoReload";
            autoReload.IsChecked = thread.autoRefresh;
            autoReload.Unchecked += (s, e) => { autoSave.IsEnabled = false; autoSave.IsChecked = false; thread.autoRefresh = false; };
            autoReload.Checked += (s, e) => { autoSave.IsEnabled = true; thread.autoRefresh = true; };
            expanderStackPanel.Children.Add(autoReload);

            autoSave.Content = "Auto-save images";
            autoSave.IsChecked = thread.saveImages;
            autoSave.Unchecked += (s, e) => { thread.saveImages = false; };
            autoSave.Checked += (s, e) => { thread.saveImages = true; };
            expanderStackPanel.Children.Add(autoSave);

            webButton.Content = "Web";
            webButton.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start("http://boards.4chan.org/" + thread.board + "/thread/" + thread.id);
            };
            webButton.Margin = new Thickness(3);
            expanderStackPanel.Children.Add(webButton);

            removeButton.Content = "Remove";
            removeButton.Click += (s, e) => 
            {
                ThreadWatchList.Children.Remove(expander);
                thread.autoRefresh = false;
                thread.autoRefreshThread.Abort();
                CloverLibrary.Global.watchFileRemove(thread);
                e.Handled = true;
            };
            removeButton.Foreground = Brushes.DarkRed;
            removeButton.Margin = new Thickness(3);
            expanderStackPanel.Children.Add(removeButton);

            ThreadWatchList.Children.Add(expander);
            CloverLibrary.Global.watchFileAdd(thread);
        }

        public void HandleUpdateThreadEvent(object sender, CloverLibrary.UpdateThreadEventArgs args)
        {
            CloverLibrary.ChanThread senderThread = (CloverLibrary.ChanThread)sender;

            switch (args.updateEvent)
            {
                case CloverLibrary.UpdateThreadEventArgs.UpdateEvent.unknown:
                    MessageBox.Show("Unknown event!");
                    if(args.context is Exception)
                    {
                        MessageBox.Show(((Exception)args.context).Message + "\n" + ((Exception)args.context).StackTrace);
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
            thread.on404();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Threading.CancellationTokenSource tokenSource = new System.Threading.CancellationTokenSource();
        public MainWindow()
        {
            InitializeComponent();
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
            tokenSource.Cancel();
            tokenSource = new System.Threading.CancellationTokenSource();

            ThreadList.Children.Clear();
            ((ScrollViewer)ThreadList.Parent).ScrollToTop();

            string board = ((Tuple<string, string, string>)((Button)sender).DataContext).Item1;
            Title = board + " - " + ((Tuple<string, string, string>)((Button)sender).DataContext).Item2;

            await CloverLibrary.Global.loadBoard(board, tokenSource.Token);

            List<CloverLibrary.ChanPost> postList = CloverLibrary.Global.getBoard(board, tokenSource.Token);
            foreach (CloverLibrary.ChanPost post in postList)
            {
                try
                {
                    post.raiseUpdateThreadEvent += HandleUpdateThreadEvent;
                    ThreadList.Children.Add(await convertPostToStackPanel(post));
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (exception.Message == "404-NotFound")
                    {
                        continue;
                    }
                    else
                    {
                        System.Diagnostics.Debugger.Break();
                        throw; 
                    }
                }
            }
        }

        public async void ThreadButton_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource.GetType() != typeof(Button))
            {
                return;
            }

            tokenSource.Cancel();
            tokenSource = new System.Threading.CancellationTokenSource();

            ThreadList.Children.Clear();
            ((ScrollViewer)ThreadList.Parent).ScrollToTop();

            CloverLibrary.ChanPost senderPost = ((CloverLibrary.ChanPost)((Button)sender).DataContext);
            await CloverLibrary.Global.loadThread(senderPost, tokenSource.Token);

            foreach (CloverLibrary.ChanPost post in senderPost.replyPosts.Values)
            {
                try
                {
                    ThreadList.Children.Add(await convertPostToStackPanel(post));
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    System.Diagnostics.Debugger.Break();
                    throw;
                }
            }
        }

        private void setGrid(UIElement element, int column = 0, int row = 0, int colSpan = 1, int rowSpan = 1)
        {
            Grid.SetColumn(element, column);
            Grid.SetRow(element, row);
            Grid.SetColumnSpan(element, colSpan);
            Grid.SetRowSpan(element, rowSpan);
        }

        private async Task<UIElement> convertPostToStackPanel(CloverLibrary.ChanPost post)
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
            textBlockSubject.Text = post.board + "/" + post.no + ((post.sub == "") ? ("") : (" - " + post.sub)) +
                " - " + post.now + (post.resto == 0 ? " - R: " + post.replies + " / I: " + post.images : "");
            textBlockSubject.TextWrapping = TextWrapping.Wrap;
            setGrid(textBlockSubject, colSpan: 2);
            retVal.Children.Add(textBlockSubject);

            await post.loadThumb(tokenSource.Token);

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
            Image toolTipImage = new Image();

            toolTipStackPanel.Orientation = Orientation.Vertical;

            toolTipTextBlock.Text = post.tim + "-" + post.filename + post.ext + " - " + post.w + " x " + post.h;
            toolTipStackPanel.Children.Add(toolTipTextBlock);

            imageToolTip.Loaded += async (ls, le) =>
            {
                await post.loadImage();
                BitmapImage imageToolTipSource = new BitmapImage();
                imageToolTipSource.BeginInit();
                imageToolTipSource.StreamSource = new System.IO.MemoryStream(post.imageData);
                imageToolTipSource.CacheOption = BitmapCacheOption.OnLoad;
                imageToolTipSource.EndInit();
                imageToolTipSource.Freeze();
                toolTipImage.Source = imageToolTipSource;
                double workingHeight = SystemParameters.WorkArea.Height * .95;
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
                    addThreadWatch(post);
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

        private void addThreadWatch(CloverLibrary.ChanPost post)
        {
            Button threadButton = new Button();
            Grid threadGrid = new Grid();
            Button title = new Button();
            CheckBox autoReload = new CheckBox();
            CheckBox autoSave = new CheckBox();
            Button removeButton = new Button();
            
            threadGrid.ColumnDefinitions.Add(new ColumnDefinition());
            threadGrid.ColumnDefinitions.Add(new ColumnDefinition());
            threadGrid.ColumnDefinitions.Add(new ColumnDefinition());
            threadGrid.RowDefinitions.Add(new RowDefinition());
            threadGrid.RowDefinitions.Add(new RowDefinition());
            threadGrid.RowDefinitions.Add(new RowDefinition());
            threadGrid.ColumnDefinitions[0].Width = GridLength.Auto;
            threadGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            threadGrid.ColumnDefinitions[2].Width = GridLength.Auto;

            title.Content = post.board + "/" + post.no + " - " + 
                (post.sub == "" ? 
                post.com.Substring(0, (post.com.Length > 50 ? 50 : post.com.Length)).Replace("\n", " ") : 
                post.sub);
            title.HorizontalContentAlignment = HorizontalAlignment.Left;
            title.Click += ThreadButton_Click;
            setGrid(title, colSpan: threadGrid.ColumnDefinitions.Count);
            threadGrid.Children.Add(title);
            ToolTip titleToolTip = new ToolTip();
            titleToolTip.Content = title.Content;
            ToolTipService.SetToolTip(title, titleToolTip);

            autoReload.Content = "AutoReload";
            autoReload.IsChecked = true;
            autoReload.Unchecked += (s, e) => { autoSave.IsEnabled = false; autoSave.IsChecked = false; post.autoRefresh = false; };
            autoReload.Checked += (s, e) => { autoSave.IsEnabled = true; post.autoRefresh = true; };
            setGrid(autoReload, row: 1);
            threadGrid.Children.Add(autoReload);

            autoSave.Content = "Auto-save images";
            autoSave.IsChecked = false;
            setGrid(autoSave, row: 2);
            threadGrid.Children.Add(autoSave);

            removeButton.Content = "Remove";
            removeButton.Click += (s, e) => 
            {
                ThreadWatchList.Children.Remove(threadButton);
                e.Handled = true;
            };
            setGrid(removeButton, column: 2, row: 2);
            threadGrid.Children.Add(removeButton);

            Rectangle rectangle = new Rectangle();
            rectangle.Fill = Brushes.Blue;
            setGrid(rectangle, column: 1, row: 1);
            threadGrid.Children.Add(rectangle); ;

            threadButton.Content = threadGrid;
            threadButton.Margin = new Thickness(2);
            threadButton.Padding = new Thickness(5);
            threadButton.DataContext = post;
            threadButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            threadGrid.MinWidth = 260;
            ThreadWatchList.Children.Add(threadButton);
        }

        public void HandleUpdateThreadEvent(object sender, CloverLibrary.UpdateThreadEventArgs args)
        {
            
        }
    }
}

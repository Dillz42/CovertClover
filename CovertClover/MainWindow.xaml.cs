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

            //CloverLibrary.Global.testDownload();

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

        public async void ThreadButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource.Cancel();
            tokenSource = new System.Threading.CancellationTokenSource();

            ThreadList.Children.Clear();
            ((ScrollViewer)ThreadList.Parent).ScrollToTop();

            CloverLibrary.ChanPost senderPost = ((CloverLibrary.ChanPost)((Button)sender).DataContext);
            await CloverLibrary.Global.loadThread(senderPost.no, senderPost.board, tokenSource.Token);

            List<CloverLibrary.ChanPost> postList = CloverLibrary.Global.getThread(senderPost.no, senderPost.board, tokenSource.Token);
            foreach (CloverLibrary.ChanPost post in postList)
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

        private async Task<FrameworkElement> convertPostToStackPanel(CloverLibrary.ChanPost post)
        {
            StackPanel retVal = new StackPanel();

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

            TextBlock textBlockSubject = new TextBlock();
            textBlockSubject.Text = post.board + "/" + post.no + ((post.sub == "") ? ("") : (" - " + post.sub)) +
                " - R: " + post.replies + " / I: " + post.images;
            textBlockSubject.TextWrapping = TextWrapping.Wrap;

            TextBlock textBlockComment = new TextBlock();
            textBlockComment.Text = post.com;
            textBlockComment.TextWrapping = TextWrapping.Wrap;

            retVal.Children.Add(textBlockSubject);
            retVal.Children.Add(img);
            retVal.Children.Add(textBlockComment);
            
            if (post.resto == 0)
            {
                Button button = new Button();

                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Content = retVal;
                button.Click += (cs, ce) =>
                {
                    Button threadButton = new Button();
                    threadButton.Content = post.board + "/" + post.no + "-" + post.sub;
                    threadButton.Click += ThreadButton_Click;
                    threadButton.DataContext = post;
                    ThreadWatchList.Children.Add(threadButton);
                };

                return button; 
            }
            else
            {
                Separator seperator = new Separator();
                retVal.Children.Add(seperator);

                return retVal;
            }
        }
    }
}

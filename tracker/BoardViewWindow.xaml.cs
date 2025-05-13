// File: BoardViewWindow.xaml.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace MyApp
{
    public partial class BoardViewWindow : Window
    {
        private DispatcherTimer _timer;
        private BoardViewModel _viewModel;
        
        public BoardViewWindow(ref Board board)
        {
            InitializeComponent();
            _viewModel = new BoardViewModel(ref board);
            DataContext = _viewModel;
            
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100); // Set the interval to 1 second
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            this.Closed += (s, e) => Environment.Exit(0);

        }
        
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update the view model here
            _viewModel.UpdateCellColors();
        }
    }
    
    public class BoardViewModel
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public ObservableCollection<CellViewModel> Cells { get; set; }
        
        private Board _board;

        public BoardViewModel(ref Board board)
        {
            _board = board;
            // Set board dimensions (use the board size)
            RowCount = board.height;
            ColumnCount = board.width;
            Cells = new ObservableCollection<CellViewModel>();

            // Initialize cells with placeholder content
            for (int y = 0; y < RowCount; y++)
            {
                for (int x = 0; x < ColumnCount; x++)
                {
                    string bg = board.people.Any(p => p.currentCell.x == x && p.currentCell.y == y) ? "Black": "White";
                    Cells.Add(new CellViewModel { X = x, Y = y, Content = $"({x},{y})", Bg = bg });
                }
            }
        }

        public void UpdateCellColors()
        {
            foreach (var cell in Cells)
            {
                var personsInCell = _board.people.Where(p => p.currentCell.x == cell.X && p.currentCell.y == cell.Y);
                
                if (personsInCell.Any())
                {
                    cell.Bg = personsInCell.Any(p => p.infected) ? "Green": "Black";
                }
                else
                {
                    cell.Bg = "White";
                }
            }
        }
    }
    
    public class CellViewModel : INotifyPropertyChanged
    {
        private string _bg;
        public int X { get; set; }
        public int Y { get; set; }
        public string Content { get; set; }

        public string Bg
        {
            get => _bg;
            set
            {
                _bg = value;
                OnPropertyChanged(nameof(Bg));
            }
            
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


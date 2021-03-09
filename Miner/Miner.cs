using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SD = System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Soap;

namespace Miner
{
    public enum Complexity { newbie = 1, amatour, profesional, special }
    public interface IStatistic
    {
        void Add(Complexity complexity, string name, int score, double time, DateTime date);
        void ShowLiders();
        void Save();
        void Load();
    }
    public interface IScoreGame
    {
        void StartGame();//фиксация начала игры
        void EndGame();//фиксация конца игры
        double GetTimeGame(); //время в "с" затраченое на игру
        int ScoreCount(Map map);//подсчет очков за игру
    }
    public interface IControlStrategy
    {
        void Start(IStatistic statistic, User currentUser, IScoreGame score);
    }
    public interface IAbstractFactoryMiner
    {
        IStatistic GetStatistic();
        IScoreGame GetScoreGame();
        IControlStrategy GetControlStrategy();
    }
    public interface IPlayedGame
    {
        int GetScoreGame();
        double GetTimeGame();
    }
    public class Miner
    {
        private IStatistic statistic;
        private IScoreGame score;
        private IControlStrategy control;
        public User CurrentUser { get; protected set; }
        public Miner(User user, IAbstractFactoryMiner factory)
        {
            CurrentUser = user;
            statistic = factory.GetStatistic();
            statistic.Load();
            control = factory.GetControlStrategy();
            score = factory.GetScoreGame();
        }
        public void Start() //основной метод для игры через стрелки
        {
            try
            {
                bool exit = false;
                if (Menu.ChooseNewGameOrLiders() == 1) statistic.ShowLiders();
                do
                {
                    control.Start(statistic, CurrentUser, score);
                    Console.WriteLine("Нажмите клавишу для продолжения...");
                    Console.ReadKey();
                    exit = Exit();
                } while (!exit);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                statistic.Save();
            }
        }
        private bool Exit()//выход
        {
            switch (Menu.ChooseExit())//меню выхода
            {
                case 1:
                    statistic.ShowLiders();
                    break;
                case 2:
                case -1:
                    return true;
            }
            return false;
        }
    }
    public abstract class AbstractControl : IControlStrategy
    {
        protected AbstractControl() { }
        public abstract void Start(IStatistic statistic, User currentUser, IScoreGame score);
        protected abstract bool OpenCell(Point point, Map map, Map mines);//открытие ячеек,если бомба, тогда возврат true
        protected abstract void OpenCellWithNull(Point point, Map map, Map mines);//открытие всех пустых смежных ячеек
        protected virtual void Initial(out Complexity currentComplexity, out Map map, out Map mines)
        {
            Console.Clear();
            int SizeMapHeight;
            int SizeMapWidth;
            int AmountOfMines;
            currentComplexity = Menu.ChooseComplexity("\tНовая игра");
            switch (currentComplexity)
            {
                case Complexity.newbie:
                    SizeMapHeight = 9;
                    SizeMapWidth = 9;
                    AmountOfMines = 10;
                    break;
                case Complexity.amatour:
                    SizeMapHeight = 16;
                    SizeMapWidth = 16;
                    AmountOfMines = 40;
                    break;
                case Complexity.profesional:
                    SizeMapHeight = 16;
                    SizeMapWidth = 30;
                    AmountOfMines = 99;
                    break;
                case Complexity.special:
                    SizeMapHeight = Menu.EnterHeightMap();
                    SizeMapWidth = Menu.EnterWidthMap();
                    AmountOfMines = Menu.EnterAmountOfMines();
                    break;
                default:
                    throw new NotImplementedException();
            }
            mines = new Map(SizeMapHeight, SizeMapWidth, AmountOfMines);
            map = new Map(SizeMapHeight, SizeMapWidth, AmountOfMines);
        }
        protected virtual void Show(Map map, User user)//базовая отрисовка игрового поля
        {
            Console.Clear();
            user.Show(map.SizeMapWidth, map.SizeMapHeight);
            map.Show();
        }
        protected virtual bool CheckWin(Map map)//проверка окончание игры победой
        {
            int CountFlags = 0;
            int CountEmptyCells = 0;
            for (int i = 0; i < map.SizeMapHeight; i++)//проход всех ячеек
            {
                for (int j = 0; j < map.SizeMapWidth; j++)
                {
                    if (map[i, j] == -2)
                    {
                        CountFlags++;
                    }
                    else if (map[i, j] == 0)
                    {
                        CountEmptyCells++;
                    }
                }
            }
            if (CountFlags == map.AmountOfMines && CountEmptyCells == 0) return true;
            return false;
        }
        protected virtual void ShowMines(Map map, Map mines)
        {
            for (int i = 0; i < map.SizeMapHeight; i++)
            {
                for (int j = 0; j < map.SizeMapWidth; j++)
                {
                    if (map[i, j] == 0 && mines[i, j] == -1)
                    {
                        map[i, j] = -1;
                    }
                }
            }
        }
    }
    public class ControlArrow : AbstractControl
    {
        public override void Start(IStatistic statistic, User currentUser, IScoreGame score) //основной метод для игры через стрелки
        {
            Map map;//'*' = -1 - mine, '^' = -2 - flag, -3 - empty cell (open), 0 - empty cell, 1,2,3,4,5,6,7,8 - numbers
            Map mines;
            bool endGame = false;
            bool bombed = false;
            bool firstMove = true;
            ConsoleKeyInfo key;
            Complexity currentComplexity;
            Initial(out currentComplexity, out map, out mines);
            Point cursor = new Point();
            Point newCursor = new Point();
            Show(map, currentUser);
            ShowInfoCursor();
            map.SetCursor(newCursor);
            do
            {
                key = Console.ReadKey();
                switch (key.Key)
                {
                    case ConsoleKey.Enter://поставить флаг
                        if (map[newCursor] != -2)//защита от повторной установки флага
                        {
                            map.CurrentMines--;
                            map[newCursor] = -2;
                        }
                        else
                        {
                            map.CurrentMines++;
                            map[newCursor] = 0;
                        }
                        map.ReShowNumberMines();
                        map.WriteSymbol(newCursor);
                        break;
                    case ConsoleKey.Spacebar://открыть ячейку
                        if (firstMove)//если первый ход то растановка мин 
                        {
                            score.StartGame();
                            mines.InitialBombs(newCursor);
                            firstMove = false;
                        }
                        if (map[newCursor] == -2)//если на открываемой ячейке стоит флаг возвращаем число мин
                        {
                            map.CurrentMines++;
                            map.ReShowNumberMines();
                        }
                        bombed = OpenCell(newCursor, map, mines);
                        break;
                    case ConsoleKey.LeftArrow://сдвиг указателя влево
                        cursor = newCursor.Clone();
                        newCursor.Letter--;
                        if (!map.IsInBorder(newCursor)) newCursor = cursor;
                        break;
                    case ConsoleKey.RightArrow://сдвиг указателя вправо
                        cursor = newCursor.Clone();
                        newCursor.Letter++;
                        if (!map.IsInBorder(newCursor)) newCursor = cursor;
                        break;
                    case ConsoleKey.UpArrow://сдвиг указателя вверх
                        cursor = newCursor.Clone();
                        newCursor.Number--;
                        if (!map.IsInBorder(newCursor)) newCursor = cursor;
                        break;
                    case ConsoleKey.DownArrow://сдвиг указателя вниз
                        cursor = newCursor.Clone();
                        newCursor.Number++;
                        if (!map.IsInBorder(newCursor)) newCursor = cursor;
                        break;
                    case ConsoleKey.Escape://выход
                        endGame = true;
                        Show(map, currentUser);
                        break;
                    default:
                        map.WriteSymbol(newCursor);
                        break;
                }
                if (!endGame)
                {
                    map.WriteSymbol(cursor);//повторная прорисовка зарисованой вводом ячейки
                    map.SetCursor(newCursor);//перемещение курсора на новую позицию
                }
                if (bombed)//вывод результата поражения
                {
                    score.EndGame();
                    ShowMines(map, mines);
                    Show(map, currentUser);
                    Console.WriteLine("Поражение!!!");
                    Console.WriteLine($"Затраченое время {score.GetTimeGame():0.00} с...");
                    endGame = true;
                }
                else if (CheckWin(map))//проверка победы, вывод результата
                {
                    score.EndGame();
                    currentUser.Score += score.ScoreCount(map);
                    statistic.Add(currentComplexity, currentUser.Name, score.ScoreCount(map), score.GetTimeGame(), DateTime.Now);
                    Show(map, currentUser);
                    Console.WriteLine("Победа!!!");
                    Console.WriteLine($"Затраченое время {score.GetTimeGame():0.00} с");
                    Console.WriteLine($"Полученые очки {score.ScoreCount(map)}");
                    endGame = true;
                }
            } while (!endGame);
        }
        protected virtual void ShowInfoCursor()
        {
            Console.WriteLine("Управление:\n" +
                "стрелки (вверх, вниз, влево, вправо) - движение по полю\n" +
                "cpace - открыть ячейку\n" +
                "enter - поставить флаг\n" +
                "esc - выход");
        }
        protected override bool OpenCell(Point point, Map map, Map mines)//открытие ячеек,если бомба, тогда возврат true
        {
            map[point] = mines[point];
            map.WriteSymbol(point);
            if (mines[point] == -1)
            {
                return true;
            }
            else if (mines[point] == 0) OpenCellWithNull(point, map, mines);
            return false;
        }
        protected override void OpenCellWithNull(Point point, Map map, Map mines)//открытие всех пустых смежных ячеек
        {
            if (map[point] == -2)
            {
                map.CurrentMines++;
                map.ReShowNumberMines();
            }
            if (mines[point] == 0)
            {
                map[point] = -3;
                mines[point] = -3;
                map.WriteSymbol(point);
                for (int i = point.Number - 1; i < point.Number + 2; i++)
                {
                    for (int j = point.Letter - 1; j < point.Letter + 2; j++)
                    {
                        if (mines.IsInBorder(new Point(i, j)) && mines[new Point(i, j)] != -3)
                        {
                            OpenCellWithNull(new Point(i, j), map, mines);
                        }
                    }
                }
            }
            map[point] = mines[point];
            map.WriteSymbol(point);
        }
    }
    
    public class ControlCoordinate: AbstractControl
    {
        public override void Start(IStatistic statistic, User currentUser, IScoreGame score) //основной метод для игры через стрелки
        {
            Map map;//'*' = -1 - mine, '^' = -2 - flag, -3 - empty cell (open), 0 - empty cell, 1,2,3,4,5,6,7,8 - numbers
            Map mines;
            bool endGame = false;
            bool bombed = false;
            bool firstMove = true;
            Complexity currentComplexity;
            Initial(out currentComplexity, out map, out mines);
            Point point = new Point();
            do
            {
                Show(map, currentUser);
                point = Menu.EnterPoint(map.SizeMapHeight, map.SizeMapWidth);
                if (Menu.ChooseActionOnCell() == 0)
                {
                    if (firstMove)
                    {
                        score.StartGame();
                        mines.InitialBombs(point);
                        firstMove = false;
                    }
                    if (map[point] == -2) map.CurrentMines++;
                    bombed = OpenCell(point, map, mines);
                }
                else if (map[point] != -2)//защита от повторной установки флага
                {
                    map[point] = -2;
                    map.CurrentMines--;
                }
                else
                {
                    map[point] = 0;
                    map.CurrentMines++;
                }
                if (bombed)//вывод результата поражения
                {
                    score.EndGame();
                    ShowMines(map, mines);
                    Show(map, currentUser);
                    Console.WriteLine("Поражение!!!");
                    Console.WriteLine($"Затраченое время {score.GetTimeGame():0.00} с...");
                    endGame = true;
                }
                else if (CheckWin(map))//проверка победы, вывод результата
                {
                    score.EndGame();
                    currentUser.Score += score.ScoreCount(map);
                    statistic.Add(currentComplexity, currentUser.Name, score.ScoreCount(map), score.GetTimeGame(), DateTime.Now);
                    Show(map, currentUser);
                    Console.WriteLine("Победа!!!");
                    Console.WriteLine($"Затраченое время {score.GetTimeGame():0.00} с");
                    Console.WriteLine($"Полученые очки {score.ScoreCount(map)}");
                    endGame = true;
                }
            } while (!endGame);
        }
        protected override bool OpenCell(Point point, Map map, Map mines)//открытие ячеек,если бомба, тогда возврат true
        {
                map[point] = mines[point];
                if (mines[point] == -1)
                {
                    return true;
                }
                else if (mines[point] == 0) OpenCellWithNull(point, map, mines);
                return false;
        }
        protected override void OpenCellWithNull(Point point, Map map, Map mines)//открытие всех пустых смежных ячеек
        {
            if (map[point] == -2)
            {
                map.CurrentMines++;
            }
            if (mines[point] == 0)
            {
                map[point] = -3;
                mines[point] = -3;
                for (int i = point.Number - 1; i < point.Number + 2; i++)
                {
                    for (int j = point.Letter - 1; j < point.Letter + 2; j++)
                    {
                        if (mines.IsInBorder(new Point(i, j)) && mines[new Point(i, j)] != -3)
                        {
                            OpenCellWithNull(new Point(i, j), map, mines);
                        }
                    }
                }
            }
            map[point] = mines[point];
        }
    }
    public class ScoreGame : IScoreGame
    {
        private long startTime;
        private long endTime;
        public ScoreGame()
        {
            startTime = 0;
            endTime = 0;
        }
        public void StartGame() => startTime = SD.Stopwatch.GetTimestamp();
        public void EndGame() => endTime = SD.Stopwatch.GetTimestamp();
        public double GetTimeGame() => //время в "с" затраченое на игру
            (endTime - startTime) / (double)SD.Stopwatch.Frequency;
        public int ScoreCount(Map map)//подсчет очков за игру
        {
            int score = (int)(200 * map.AmountOfMines / (double)map.SizeMapHeight / (double)map.SizeMapWidth);//очки за сложность
            score += (int)(CountCellWithNumber(map) / (Math.Log(GetTimeGame())));//очки за время
            return score;
        }
        private int CountCellWithNumber(Map map)//подсчет ячеек с цифрами (для определения очков за время)
        {
            int count = 0;
            for (int i = 0; i < map.SizeMapHeight; i++)
            {
                for (int j = 0; j < map.SizeMapWidth; j++)
                {
                    if (map[i, j] > 0) count++;
                }
            }
            return count;
        }
    }
    public class Map//поле игры
    {
        public int SizeMapHeight { get; set; }
        public int SizeMapWidth { get; set; }
        int[,] map;//'*' = -1 - mine, '^' = -2 - flag, 1,2,3,4,5,6,7,8 - numbers
        public int AmountOfMines { get; set; }
        public int CurrentMines { get; set; }
        public Map(int sizeMapHeight, int sizeMapWidth, int amountOfMines)
        {
            SizeMapHeight = sizeMapHeight;
            SizeMapWidth = sizeMapWidth;
            AmountOfMines = CurrentMines = amountOfMines;
            map = new int[SizeMapHeight, SizeMapWidth];
        }
        public Map() : this(9, 9, 10) { }
        public void InitialBombs(Point point)//point - координата первого хода //растановка бомб
        {
            Random rand = new Random();
            Point tempPoint = new Point();
            for (int i = 0; i < AmountOfMines; i++)
            {
                do
                {
                    tempPoint.Number = rand.Next(SizeMapHeight);
                    tempPoint.Letter = rand.Next(SizeMapWidth);
                } while (map[tempPoint.Number, tempPoint.Letter] == -1 || tempPoint.Equals(point));
                map[tempPoint.Number, tempPoint.Letter] = -1;
            }
            CountCellBomb();
        }
        private void CountCellBomb()//заполнение карты цифрами подсчета количества бомб вокруг ячейки
        {
            for (int i = 0; i < SizeMapHeight; i++)//поиск ячейки с бомбой
            {
                for (int j = 0; j < SizeMapWidth; j++)
                {
                    if (map[i, j] == -1)
                    {
                        CountBombs(new Point(i, j));
                    }
                }
            }
        }
        private void CountBombs(Point point)//проход ячеек вокруг бомбы с увеличением числа всех смежных ячеек на 1
        {
            for (int i = point.Number - 1; i < point.Number + 2; i++)
            {
                for (int j = point.Letter - 1; j < point.Letter + 2; j++)
                {
                    if (IsInBorder(new Point(i, j)))
                    {
                        if (map[i, j] != -1)
                        {
                            (map[i, j])++;
                        }
                    }
                }
            }
        }
        public bool IsInBorder(Point point)//проверка граници поля (для исключения изменения ячейки)
        {
            if (point.Number < 0 || point.Letter < 0 || point.Number > SizeMapHeight - 1 || point.Letter > SizeMapWidth - 1)
            {
                return false;//точка вне границ заданого поля
            }
            return true;//точка в границах заданого поля
        }
        public void Show()//отрисовка поля с минами
        {
            ShowBorderLetters();
            ShowHorizontalBorder();
            for (int i = 0; i < SizeMapHeight; i++)
            {
                ShowLeftBorder(i);
                for (int j = 0; j < SizeMapWidth; j++)
                {
                    Console.Write(GetSymbol(map[i, j]) + " ");
                    if (SizeMapWidth > 26) Console.Write(" ");
                }
                Console.WriteLine("|");
            }
            ShowHorizontalBorder();
            Menu.ShowSpaces((SizeMapHeight.ToString().Length + SizeMapWidth * (2 + SizeMapWidth / 26)
                - 13 - AmountOfMines.ToString().Length) / 2);//отрисовка пробелов для центровки информации
            Console.WriteLine($"Оставшихся мин: {CurrentMines:00}\n");
        }
        public void ReShowNumberMines()//повторный показ колличества оставшихся мин (при управлении стрелками)
        {
            int number = 4 + SizeMapHeight;
            int letter = (SizeMapHeight.ToString().Length + SizeMapWidth * (2 + SizeMapWidth / 26)
                - 13 - AmountOfMines.ToString().Length) / 2 + 16;
            Console.SetCursorPosition(letter, number);
            Console.Write("{0:00}", CurrentMines);
        }
        private void ShowLeftBorder(int indexRow)//отрисовка левой границы поля (с цифрами)
        {
            if (SizeMapHeight > 9 && indexRow < 9) Console.Write(" ");//если число строк двухзначное и номер строки меньше "10" добавляем пустую ячейку
            Console.Write($"{indexRow + 1}| ");
        }
        private void ShowBorderLetters()//отрисовка верхней линии с метками столбцов (буквы)
        {
            if (SizeMapHeight > 9) Console.Write(" ");
            Console.Write("  ");
            for (int i = 0; i < SizeMapWidth; i++)
            {
                if (i < 26) Console.Write(" ");
                else Console.Write((char)('a' + (i) / 26 - 1));
                Console.Write((char)('a' + i % 26));//literals: a,b,c,d...
                if (SizeMapWidth > 26) Console.Write(" ");
            }
            Console.WriteLine(" ");
        }
        private void ShowHorizontalBorder()//отрисовка горизонтальной граници поля с минами
        {
            if (SizeMapHeight > 9) Console.Write(" ");
            Console.Write(" +-");
            for (int i = 0; i < SizeMapWidth; i++)
            {
                Console.Write("--");
                if (SizeMapWidth > 26) Console.Write("-");
            }
            Console.WriteLine("+");
        }
        private char GetSymbol(int positionMap)//парсер с кода символа в символ char
        {
            switch (positionMap)
            {
                case -1:
                    return '*';//mine
                case -2:
                    return '^';//flag
                case 0:
                    return ' ';//empty
                case -3:
                    return (char)183;//empty (open)
                default:
                    return (char)(positionMap + 48);//1,2,3,4,5,6,7,8
            }
        }
        public int this[int i, int j]//доступ к ячейке поля
        {
            get
            {
                return map[i, j];
            }
            set
            {
                if (i < 0 || i >= SizeMapHeight || j < 0 || j >= SizeMapWidth) throw new IndexOutOfRangeException();
                map[i, j] = value;
            }
        }
        public int this[Point point]//доступ к ячейке поля
        {
            get
            {
                return map[point.Number, point.Letter];
            }
            set
            {
                map[point.Number, point.Letter] = value;
            }
        }
        private int XCursor(Point p) =>//определения позиции курсора по горизонтали (letter)
            SizeMapHeight.ToString().Length + 2 + (p.Letter) * (2 + SizeMapWidth / 26);
        private int YCursor(Point p) =>//определение позиции курсора по вертикали (number)
            3 + p.Number;
        public void WriteSymbol(Point p)//перезапись (отриовка) на поле ячейки
        {
            Console.SetCursorPosition(XCursor(p), YCursor(p));
            Console.Write(GetSymbol(map[p.Number, p.Letter]));
        }
        public void SetCursor(Point p)//установка курсора на экране по игровой координате
        {
            Console.SetCursorPosition(XCursor(p), YCursor(p));
        }
    }
    public struct Point
    {
        public int Letter { get; set; }
        public int Number { get; set; }
        public Point(int number, int letter)
        {
            Number = number;
            Letter = letter;
        }
        public Point Clone() => new Point(Number, Letter);
        public override string ToString() => $"{Number},{Letter}";
        public override bool Equals(object obj) => this.ToString().Equals(obj.ToString());
        public override int GetHashCode() => this.ToString().GetHashCode();
        public static bool operator ==(Point p1, Point p2) => p1.Equals(p2);
        public static bool operator !=(Point p1, Point p2) => !(p1 == p2);
    }
    public static class Menu
    {
        public static Point EnterPoint(int sizeMapHeight, int SizeMapWidth)//ввод с клавиатуры координаты ячейки на поле Мар
        {
            Point point = new Point();
            string str;//строка для обработки
            int index = -1;//позиция в строке разделителя координат ','
            int number = sizeMapHeight + 1;//координата по высоте
            int letter = SizeMapWidth + 1;//координата в ширину (буква)
            do
            {
                Console.WriteLine("Введите координаты точки (пример 9,c)");
                str = Console.ReadLine();
                index = str.IndexOf(',');
                if (index != -1)
                {
                    int.TryParse(str.Substring(0, index), out number);
                    if (SizeMapWidth < 27 || str.Substring(index + 1).Length == 1) // если размер карты в ширину меньше количества букв или если ввели только 1 букву
                    {
                        letter = str.ToLower()[index + 1] - 'a' + 1;//переводим буквенный ввод (1 буква) в число
                    }
                    else letter = (str.ToLower()[index + 1] - 'a' + 1) * 26 + str.ToLower()[index + 2] - 'a' + 1;//переводим буквенный ввод (2 буквы) в число
                }
            } while (index == -1 || number > sizeMapHeight || letter > SizeMapWidth || number < 1 || letter < 1);
            point.Number = number - 1;
            point.Letter = letter - 1;
            return point;
        }
        public static int ChooseActionOnCell() =>//выбор действия для ячейки
            Menu.MultipleChoiceNumbers(false, "Введите:",
                "открыть ячейку",
                "поставить флаг на бомбу (^)");
        public static int ChooseExit() =>//проверка выхода из игры
            Menu.MultipleChoice(true, "Введите:",
                "начать заного",
                "просмотреть таблицу лидеров",
                "завершить игру");
        public static void ShowSpaces(int count)
        {
            if (count > 0) Console.Write(new string(' ', count));
        }
        public static Complexity ChooseComplexity(string message) =>//меню выбора сложности
            (Complexity)(Menu.MultipleChoice(false,
            message + "\nВыберите сложность:",
            "новичок",
            "любитель",
            "профессионал",
            "особый") + 1);
        public static int ChooseNewGameOrLiders() =>//стартовое меню
            Menu.MultipleChoice(false, "Выберите:",
            "новая игра",
            "просмотреть таблицу лидеров");
        public static int EnterWidthMap()//ввод ширины поля (letter) при пользовательской игре
        {
            int width;
            do
            {
                Console.WriteLine("Введите ширину поля:");
            } while (!int.TryParse(Console.ReadLine(), out width));
            return width;
        }
        public static int EnterHeightMap()//ввод высоты поля (number) при пользовательской игре
        {
            int height;
            do
            {
                Console.WriteLine("Введите высоту поля:");
            } while (!int.TryParse(Console.ReadLine(), out height));
            return height;
        }
        public static int EnterAmountOfMines()//ввод колличества мин при пользовательской игре
        {
            int mines;
            do
            {
                Console.WriteLine("Введите колличество мин:");
            } while (!int.TryParse(Console.ReadLine(), out mines));
            return mines;
        }
        public static int MultipleChoice(bool canCancel, string message, params string[] options)
        {
            int optionsPerLine = options.Length;
            int currentSelection = 0;
            ConsoleKey key;
            Console.CursorVisible = false;
            do
            {
                Console.Clear();
                if (message != null) Console.WriteLine(message);
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentSelection)
                        Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine(options[i]);

                    Console.ResetColor();
                }
                key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        {
                            if (currentSelection > 0)
                                currentSelection--;
                            break;
                        }
                    case ConsoleKey.DownArrow:
                        {
                            if (currentSelection < optionsPerLine - 1)
                                currentSelection++;
                            break;

                        }
                    case ConsoleKey.Escape:
                        {
                            if (canCancel)
                                return -1;
                            break;
                        }
                }
            } while (key != ConsoleKey.Enter);
            Console.CursorVisible = true;
            return currentSelection;
        }
        public static int MultipleChoiceNumbers(bool canCancel, string message, params string[] options)
        {
            int curSel = 0;
            ConsoleKey key;
            do
            {
                if (message != null) Console.WriteLine($"{message}\nВведите для выбора:");
                for (int i = 1; i <= options.Length; i++)
                {
                    Console.WriteLine($"{i}. {options[i - 1]}");
                }
                key = Console.ReadKey(true).Key;
                if (canCancel && key == ConsoleKey.Escape) return -1;
            } while (!int.TryParse(key.ToString().Remove(0, 1), out curSel) || curSel < 1 || curSel > options.Length);
            return curSel - 1;
        }
        public static string EnterValue(string message)
        {
            Console.Clear();
            Console.Write(message);
            return Console.ReadLine();
        }
    }
    public class User
    {
        public string Name { get; protected set; }
        private int score;
        public int Score
        {
            get => score;
            set
            {
                if (value >= 0) score = value;
                else throw new ArgumentException("Cчет не может быть отрицательным");
            }
        }
        public User (string name, int score)
        {
            Name = name;
            Score = score;
        }
        public User() : this("User", 0) { }
        public void Show(int widthMap, int heightMap)//отрисовка на поле данных пользователя
        {
            Menu.ShowSpaces((heightMap.ToString().Length + widthMap * (2 + widthMap / 26) 
                - 11 - (Name+score.ToString()).Length) / 2);//отрисовка пробелов для центровки информации
            Console.WriteLine($"Игрок: {Name} очки: {Score}");
        }
    }
    
    [Serializable]
    public class GradeStatistic : IStatistic//данные статистики игры
    {
        private List<IPlayedGame> newbie;
        private List<IPlayedGame> amateur;
        private List<IPlayedGame> professional;
        private List<IPlayedGame> special;
        public GradeStatistic()
        {
            newbie = new List<IPlayedGame>(11);
            amateur = new List<IPlayedGame>(11);
            professional = new List<IPlayedGame>(11);
            special = new List<IPlayedGame>(11);
        }
        private void AddNewbie(IPlayedGame game)//добавление записи в таблицу "новичек"
        {
            if (newbie.Count == 0 || game.GetTimeGame() < newbie.LastOrDefault().GetTimeGame()) 
            {
                newbie.Add(game);
                newbie = newbie.OrderBy(t => t.GetTimeGame()).ToList();
                if (newbie.Count == 11)
                {
                    newbie.RemoveAt(10);
                }
            }
        }
        private void AddAmateur(IPlayedGame game)//добавление записи в таблицу "любитель"
        {
            if (amateur.Count == 0 || game.GetTimeGame() < amateur.LastOrDefault().GetTimeGame())
            {
                amateur.Add(game);
                amateur = amateur.OrderBy(t => t.GetTimeGame()).ToList();
                if (amateur.Count == 11)
                {
                    amateur.RemoveAt(10);
                }
            }
        }
        private void AddProfesional(IPlayedGame game)//добавление записи в таблицу "профессионал"
        {
            if (professional.Count == 0 || game.GetTimeGame() < professional.LastOrDefault().GetTimeGame())
            {
                professional.Add(game);
                professional = professional.OrderBy(t => t.GetTimeGame()).ToList();
                if (professional.Count == 11)
                {
                    professional.RemoveAt(10);
                }
            }
        }
        private void AddSpecial(IPlayedGame game)//добавление записи в таблицу "особое"
        {
            if (special.Count == 0 || game.GetScoreGame() > special.LastOrDefault().GetScoreGame())
            {
                special.Add(game);
                special = special.OrderByDescending(t => t.GetScoreGame()).ToList();
                if (special.Count == 11)
                {
                    special.RemoveAt(10);
                }
            }
        }
        public void Add(Complexity complexity, string name, int score, double time, DateTime date)//добавление данных в таблици лидеров
        {
            if (name == null) throw new ArgumentNullException();
            switch (complexity)
            {
                case Complexity.newbie:
                    AddNewbie(new PlayedGame(name, score, time, date));
                    break;
                case Complexity.amatour:
                    AddAmateur(new PlayedGame(name, score, time, date));
                    break;
                case Complexity.profesional:
                    AddProfesional(new PlayedGame(name, score, time, date));
                    break;
                case Complexity.special:
                    AddSpecial(new PlayedGame(name, score, time, date));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        public void ShowLiders()//отрисовка таблици лидеров
        {
            Complexity comp = Menu.ChooseComplexity(null);
            Console.Clear();
            int i = 1;
            switch (comp)
            {
                case Complexity.newbie:
                    Console.WriteLine("\tТаблица лидеров сложности \"Новичек\"");
                    foreach (IPlayedGame item in newbie)
                    {
                        Console.WriteLine($"{i++}. {item}");
                    }
                    break;
                case Complexity.amatour:
                    Console.WriteLine("\tТаблица лидеров сложности \"Любитель\"");
                    foreach (IPlayedGame item in amateur)
                    {
                        Console.WriteLine($"{i++}. {item}");
                    }
                    break;
                case Complexity.profesional:
                    Console.WriteLine("\tТаблица лидеров сложности \"Профессионал\"");
                    foreach (IPlayedGame item in professional)
                    {
                        Console.WriteLine($"{i++}. {item}");
                    }
                    break;
                case Complexity.special:
                    Console.WriteLine("\tТаблица лидеров сложности \"Особое\"");
                    foreach (IPlayedGame item in special)
                    {
                        Console.WriteLine($"{i++}. {item}");
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
            Console.Write("Нажмите клавишу для продолжения...");
            Console.ReadKey();
        }
        public void Save()
        {
            if (!Directory.Exists("data")) Directory.CreateDirectory("data");
            SoapFormatter sf = new SoapFormatter();
            Save(newbie, "data\\minerNewbie.soap", sf);
            Save(amateur, "data\\minerAmateur.soap", sf);
            Save(professional, "data\\minerProfessional.soap", sf);
            Save(special, "data\\minerSpecial.soap", sf);
        }
        private void Save(List<IPlayedGame> list, string path, SoapFormatter sf)
        {
            using (Stream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                sf.Serialize(fs, list.ToArray());
            }
        }
        public void Load()
        {
            if (Directory.Exists("data"))
            {
                SoapFormatter sf = new SoapFormatter();
                newbie = Load("data\\minerNewbie.soap", sf);
                amateur = Load("data\\minerAmateur.soap", sf);
                professional = Load("data\\minerProfessional.soap", sf);
                special = Load("data\\minerSpecial.soap", sf);
            }
        }
        private List<IPlayedGame> Load(string path, SoapFormatter sf)
        {
            if (File.Exists(path))
            {
                using (Stream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    return (sf.Deserialize(fs) as IPlayedGame[]).ToList<IPlayedGame>();
                }
            }
            else return new List<IPlayedGame>();
        }
    }
    [Serializable]
    public class SingleStatistic : IStatistic//данные статистики игры
    {
        private List<IPlayedGame> statistic;
        public SingleStatistic()
        {
            statistic = new List<IPlayedGame>(11);
        }
        private void AddStatistic(IPlayedGame game)//добавление записи в таблицу
        {
            if (statistic.Count == 0 || game.GetScoreGame() > statistic.LastOrDefault().GetScoreGame())
            {
                statistic.Add(game);
                statistic = statistic.OrderByDescending(t => t.GetScoreGame()).ToList();
                if (statistic.Count == 11)
                {
                    statistic.RemoveAt(10);
                }
            }
        }
        public void Add(Complexity complexity, string name, int score, double time, DateTime date)//добавление данных в таблици лидеров
        {
            if (name == null) throw new ArgumentNullException();
            AddStatistic(new ComplexityPlayedGame(new PlayedGame(name, score, time, date), complexity));
        }
        public void ShowLiders()//отрисовка таблици лидеров
        {
            Console.Clear();
            int i = 1;
            Console.WriteLine("\tТаблица лидеров");
            foreach (IPlayedGame item in statistic)
            {
                Console.WriteLine($"{i++}. {item}");
            }
            Console.Write("Нажмите клавишу для продолжения...");
            Console.ReadKey();
        }
        public void Save()
        {
            if (!Directory.Exists("data")) Directory.CreateDirectory("data");
            SoapFormatter sf = new SoapFormatter();
            using (Stream fs = new FileStream("data\\minerSingle.soap", FileMode.OpenOrCreate, FileAccess.Write))
            {
                sf.Serialize(fs, statistic.ToArray());
            }
        }
        public void Load()
        {
            if (Directory.Exists("data") && File.Exists("data\\minerSingle.soap"))
            {
                SoapFormatter sf = new SoapFormatter();
                using (Stream fs = new FileStream("data\\minerSingle.soap", FileMode.Open, FileAccess.Read))
                {
                    statistic.AddRange((sf.Deserialize(fs) as IPlayedGame[]));
                }
            }
        }
    }
    [Serializable]
    public class PlayedGame : IPlayedGame
    {
        public string NamePlayer { get; private set; }
        public int ScoreGame { get; private set; }
        public double TimeGame { get; private set; }
        public DateTime Date { get; private set; }
        public PlayedGame(string name, int score, double time, DateTime date)
        {
            NamePlayer = name;
            ScoreGame = score;
            TimeGame = time;
            Date = date;
        }
        public override string ToString() => 
            $"Игрок: {NamePlayer} время {TimeGame:0.00} очки {ScoreGame} дата {Date:G}";
        public virtual int GetScoreGame() => ScoreGame;
        public virtual double GetTimeGame() => TimeGame;
    }
    [Serializable]
    public abstract class PlayedGameDecorator : IPlayedGame
    {
        protected IPlayedGame playedGame;
        protected PlayedGameDecorator(IPlayedGame playedGame) =>
            this.playedGame = playedGame;
        public override string ToString() => playedGame.ToString();
        public virtual int GetScoreGame() => playedGame.GetScoreGame();
        public virtual double GetTimeGame() => playedGame.GetTimeGame();
    }
    [Serializable]
    public class ComplexityPlayedGame : PlayedGameDecorator
    {
        private Complexity complexity;
        public ComplexityPlayedGame(IPlayedGame playedGame, Complexity complexity) : base(playedGame) =>
            this.complexity = complexity;
        public override string ToString() 
        {
            switch (complexity)
            {
                case Complexity.newbie:
                    return playedGame.ToString() + " сложность \"Новичек\"";
                case Complexity.amatour:
                    return playedGame.ToString() + " сложность \"Любитель\"";
                case Complexity.profesional:
                    return playedGame.ToString() + " сложность \"Професcионал\"";
                case Complexity.special:
                    return playedGame.ToString() + " сложность \"Особое\"";
                default:
                    throw new NotImplementedException();
            }
        }
    }
    public class SingleStatControlArrowFactory : IAbstractFactoryMiner
    {
        public IStatistic GetStatistic() => new SingleStatistic();
        public IScoreGame GetScoreGame() => new ScoreGame();
        public IControlStrategy GetControlStrategy() => new ControlArrow();
    }
    public class GradeStatControlCoodrinateFactory : IAbstractFactoryMiner
    {
        public IStatistic GetStatistic() => new GradeStatistic();
        public IScoreGame GetScoreGame() => new ScoreGame();
        public IControlStrategy GetControlStrategy() => new ControlCoordinate();
    }
    public class SingleStatControlCoodrinateFactory : IAbstractFactoryMiner
    {
        public IStatistic GetStatistic() => new SingleStatistic();
        public IScoreGame GetScoreGame() => new ScoreGame();
        public IControlStrategy GetControlStrategy() => new ControlCoordinate();
    }
    public class GradeStatControlArrowFactory : IAbstractFactoryMiner
    {
        public IStatistic GetStatistic() => new GradeStatistic();
        public IScoreGame GetScoreGame() => new ScoreGame();
        public IControlStrategy GetControlStrategy() => new ControlArrow();
    }
}

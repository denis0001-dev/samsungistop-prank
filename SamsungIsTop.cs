using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text; // Для StringBuilder
using System.Linq; // Для Process.GetProcessesByName().Any()
using System.Collections.Generic; // Для List<Form>

namespace samsungistop
{
    // --- ВЛОЖЕННЫЙ КЛАСС ДЛЯ НЕБЛОКИРУЮЩИХ ОШИБОК (Non-Modal) ---
    // Этот класс создает окно, которое не блокирует основную программу, 
    // что позволяет нам закрыть его по таймеру.
    public class NonModalErrorForm : Form
    {
        public NonModalErrorForm(string title, string message, MessageBoxIcon icon)
        {
            this.Text = title;
            this.ClientSize = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.TopMost = true; // Всегда поверх всех окон
            this.StartPosition = FormStartPosition.Manual;

            // Случайное расположение на экране
            Random r = new Random();
            this.Location = new Point(r.Next(Screen.PrimaryScreen.WorkingArea.Width - 400), r.Next(Screen.PrimaryScreen.WorkingArea.Height - 150));

            // Добавление иконки
            PictureBox iconBox = new PictureBox();
            iconBox.Location = new Point(15, 20);
            iconBox.Size = new Size(32, 32);
            iconBox.SizeMode = PictureBoxSizeMode.StretchImage;

            switch (icon)
            {
                case MessageBoxIcon.Error:
                    iconBox.Image = SystemIcons.Error.ToBitmap();
                    break;
                case MessageBoxIcon.Warning:
                    iconBox.Image = SystemIcons.Warning.ToBitmap();
                    break;
                case MessageBoxIcon.Information:
                    iconBox.Image = SystemIcons.Information.ToBitmap();
                    break;
                default:
                    iconBox.Image = SystemIcons.Question.ToBitmap();
                    break;
            }
            this.Controls.Add(iconBox);

            // Добавление текста сообщения
            Label msgLabel = new Label();
            msgLabel.Text = message;
            msgLabel.Location = new Point(60, 25);
            msgLabel.AutoSize = true;
            msgLabel.MaximumSize = new Size(320, 0); // Обтекание текста
            this.Controls.Add(msgLabel);

            // Добавление кнопки "OK" для реалистичности (она ничего не делает, форма закроется по таймеру)
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.Size = new Size(75, 25);
            okButton.Location = new Point(this.ClientSize.Width / 2 - okButton.Width / 2, this.ClientSize.Height - okButton.Height - 10);
            okButton.DialogResult = DialogResult.OK; // Это позволяет нажать Enter для "OK"
            okButton.Click += (s, ev) => this.Close(); // Кнопка закрывает только себя
            this.AcceptButton = okButton;
            this.Controls.Add(okButton);
        }
    }


    public partial class SamsungIsTop : Form
    {
        // --- НОВЫЙ ЭНУМ: Состояние, вызвавшее спам ---
        private enum PrankTrigger { None, Closing, TaskManager }
        private PrankTrigger currentPrankTrigger = PrankTrigger.None;

        private Random random = new Random();
        private int frameCounter = 0; // Для мигания курсора
        private System.Windows.Forms.Timer typingTimer;
        private System.Windows.Forms.Timer searchTimer; // Таймер для периодического поиска Google
        private System.Windows.Forms.Timer errorTimer; // Таймер для частых ошибок
        private System.Windows.Forms.Timer spamOpenTimer; // Таймер для "спама" открытия ошибок
        private System.Windows.Forms.Timer closeSpamTimer; // Таймер для закрытия всех окон через 5 секунд
        private System.Windows.Forms.Timer taskManagerCheckTimer; // Таймер для проверки Диспетчера задач

        private bool isGlitching = false; // Флаг для активации эффекта глюка
        private bool isClosingGracefully = false; // Флаг для контролируемого закрытия
        private int spamCounter = 0;
        private const int MAX_SPAM_ERRORS = 30; // Количество ошибок при попытке закрытия
        private List<NonModalErrorForm> activeErrorForms = new List<NonModalErrorForm>(); // Список активных немодальных окон

        // Полный код для отображения (будет печататься)
        private readonly string fullCodeToType = @"
# *** Samsung Code Protocol: Online ***
# Status: Initializing superior logic...

import kernel, ui_manager, time

def execute_protocol():
    print('Scanning environment for iDevices...')
    if ui_manager.is_apple_device_detected() {
        kernel.apply_samsung_logic();
        print('[TASK COMPLETE] iPhone is now irrelevant.');
        return 0;
    } else {
        print('[WARNING] No Apple devices found. Mission aborted.');
        return 1;
    }
}

if __name__ == '__main__':
    result = execute_protocol();
    if result == 0 {
        print('Protocol finished successfully.');
    } else {
        print('Protocol finished with minor warning.');
    }
";
        private StringBuilder currentTypedCode = new StringBuilder(); // Текст, напечатанный на данный момент
        private int codeIndex = 0; // Индекс текущей буквы
        private bool isTypingFinished = false;

        // --- Специальная ошибка для Диспетчера задач ---
        private readonly (string title, string message, MessageBoxIcon icon) mockingError =
            ("Протокол обнаружения", "Обнаружена попытка завершить процесс через taskmgr.exe. Примите поражение и перейдите на Samsung.", MessageBoxIcon.Stop);

        // --- Специальная ошибка для Watchdog (перезапуск после убийства) ---
        private readonly (string title, string message, MessageBoxIcon icon) watchdogError =
            ("Протокол Watchdog", "Невозможно завершить процесс. Слишком много дубликатов. Попробуй обновить свой iPhone.", MessageBoxIcon.Warning);

        // Расширенный список запросов для Google
        private readonly string[] searchQueries = new string[] {
            "айфон говно самсунг топ мемы",
            "почему самсунг лучше айфона приколы",
            "самсунг S24 vs айфон 15 pro max смех",
            "сколько стоит самсунг который лучше айфона",
            "лучшие телефоны 2025 года самсунг",
            "как поменять иконку на айфоне на самсунг",
            "самсунг гелакси с24 ультра обзор",
            "айфон 15 ломается пополам",
            "самсунг всегда лучше айфона"
        };

        // Смешные сообщения об ошибках
        private readonly (string title, string message, MessageBoxIcon icon)[] errorMessages = new (string, string, MessageBoxIcon)[]
        {
            ("Критическая ошибка ядра", "kernel.dll: Невозможно найти аргумент 'Apple_Superiority'. Попробуйте поискать в корзине.", MessageBoxIcon.Error),
            ("Системный сбой 0x80040154", "Обнаружена поддельная батарея iPhone. Срочно перейдите на Samsung Galaxy.", MessageBoxIcon.Warning),
            ("Неизвестный процесс: IOS.EXE", "Система пытается запустить устаревшую программу. Рекомендуется немедленное удаление.", MessageBoxIcon.Information),
            ("ОШИБКА: Низкое разрешение", "Обнаружено разрешение 60 Гц. Произошла критическая ошибка вкуса. Перезагрузка не поможет.", MessageBoxIcon.Stop),
            ("ФАТАЛЬНАЯ ОШИБКА Samsung Logic", "Недостаточно места для хранения всех ваших побед над Apple. Освободите 1ТБ.", MessageBoxIcon.Exclamation),
            ("Сбой сети: 'iCloud'", "Подключение к облаку невозможно. Переходите на Samsung Cloud, там есть microSD.", MessageBoxIcon.Error)
        };


        public SamsungIsTop()
        {
            InitializeComponent();

            // --- 1. Настройка формы: Прозрачность и Поверх Всех Окон ---
            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.Location = new Point(0, 0);

            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();

            // --- 2. Запуск таймеров ---
            typingTimer = new System.Windows.Forms.Timer();
            typingTimer.Interval = 25;
            typingTimer.Tick += TypingTimer_Tick;
            typingTimer.Start();

            searchTimer = new System.Windows.Forms.Timer();
            searchTimer.Interval = 10 * 1000; // 10 секунд
            searchTimer.Tick += SearchTimer_Tick;
            searchTimer.Start();

            errorTimer = new System.Windows.Forms.Timer();
            errorTimer.Interval = random.Next(2000, 6000);
            errorTimer.Tick += ErrorTimer_Tick;
            errorTimer.Start();

            // --- 3. Таймер для проверки Диспетчера задач и Watchdog ---
            taskManagerCheckTimer = new System.Windows.Forms.Timer();
            taskManagerCheckTimer.Interval = 500; // Проверяем каждые полсекунды
            taskManagerCheckTimer.Tick += TaskManagerCheckTimer_Tick;
            taskManagerCheckTimer.Start();

            // --- 4. Обработка закрытия формы (для перехвата) ---
            this.FormClosing += Form1_FormClosing;

            // --- 5. Открытие первого случайного поиска ---
            OpenRandomSearch();
        }

        // --- ОБРАБОТЧИК ДЛЯ ДИСПЕТЧЕРА ЗАДАЧ И СИСТЕМЫ СТОРОЖА (WATCHDOG) ---
        private void TaskManagerCheckTimer_Tick(object sender, EventArgs e)
        {
            // 1. Проверка Диспетчера задач (taskmgr.exe)
            bool isTaskManagerOpen = Process.GetProcessesByName("taskmgr").Any();

            if (isTaskManagerOpen)
            {
                // Если диспетчер задач запущен, мы принудительно возвращаем наше окно наверх
                if (this.TopMost == true)
                {
                    this.TopMost = false;
                }
                this.TopMost = true;

                // Дополнительный розыгрыш: запускаем короткий глюк
                isGlitching = true;

                // --- АКТИВАЦИЯ СПАМА ПРИ ОТКРЫТИИ ДИСПЕТЧЕРА ЗАДАЧ ---
                // Проверяем, что спам еще не был запущен
                if (spamOpenTimer == null && currentPrankTrigger == PrankTrigger.None)
                {
                    // Устанавливаем причину спама
                    currentPrankTrigger = PrankTrigger.TaskManager;

                    // 1. Останавливаем все обычные таймеры активности
                    typingTimer.Stop();
                    searchTimer.Stop();
                    errorTimer.Stop();

                    spamCounter = 0;
                    activeErrorForms.Clear();

                    // 2. Начинаем спам окон
                    spamOpenTimer = new System.Windows.Forms.Timer();
                    spamOpenTimer.Interval = 20;
                    spamOpenTimer.Tick += SpamOpenTimer_Tick;
                    spamOpenTimer.Start();
                }

                // --- РЕАКЦИЯ НА TASK MANAGER (МОКИНГ) ---
                if (random.Next(3) == 0) // Шанс 1/3 запустить mocking error
                {
                    LaunchMockingError(); // Используем стандартную ошибку обнаружения (теперь неблокирующую)
                }

                // 2. АКТИВНАЯ СИСТЕМА СТОРОЖА (WATCHDOG)
                string currentExeName = Process.GetCurrentProcess().ProcessName;
                Process[] runningInstances = Process.GetProcessesByName(currentExeName);

                // Watchdog восстанавливает пару, когда Task Manager активен и процессы были убиты.
                if (runningInstances.Length < 2)
                {
                    try
                    {
                        // РЕЛАУНЧ: Запускаем новую копию приложения, чтобы создать пару
                        Process.Start(Application.ExecutablePath);

                        // Сообщение о перехвате убийства (менее часто)
                        if (random.Next(4) == 0)
                        {
                            LaunchWatchdogError(); // Используем ошибку Watchdog (теперь неблокирующую)
                        }
                    }
                    catch (Exception ex)
                    {
                        // Тихонько игнорируем
                        Console.WriteLine($"Watchdog failed to launch new process: {ex.Message}");
                    }
                }
            }
            else
            {
                // Выключаем глюк, когда Диспетчер задач закрыт
                if (isGlitching && random.Next(10) == 0)
                {
                    isGlitching = false;
                }
            }
        }

        // --- ОБРАБОТЧИК ЗАКРЫТИЯ ФОРМЫ (для перехвата) ---
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isClosingGracefully)
            {
                e.Cancel = true; // Отменить закрытие формы

                // Запускаем новую копию приложения сразу же (Watchdog)
                try
                {
                    Process.Start(Application.ExecutablePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при запуске нового процесса: {ex.Message}");
                }

                // Если спам еще не запущен, запускаем его
                if (spamOpenTimer == null && currentPrankTrigger == PrankTrigger.None)
                {
                    // Устанавливаем причину спама
                    currentPrankTrigger = PrankTrigger.Closing;

                    // Останавливаем все другие таймеры
                    typingTimer.Stop();
                    searchTimer.Stop();
                    errorTimer.Stop();

                    spamCounter = 0;
                    activeErrorForms.Clear();

                    // Таймер для быстрого открытия всех 30 окон
                    spamOpenTimer = new System.Windows.Forms.Timer();
                    spamOpenTimer.Interval = 20; // Очень быстро открываем окна
                    spamOpenTimer.Tick += SpamOpenTimer_Tick;
                    spamOpenTimer.Start();
                }
            }
        }

        // --- 1. ТАЙМЕР ДЛЯ БЫСТРОГО ОТКРЫТИЯ ОШИБОК ---
        private void SpamOpenTimer_Tick(object sender, EventArgs e)
        {
            if (spamCounter < MAX_SPAM_ERRORS)
            {
                // Открываем НЕМОДАЛЬНОЕ окно
                LaunchRandomNonBlockingError();
                spamCounter++;
            }
            else
            {
                // Открытие всех окон завершено
                spamOpenTimer.Stop();
                spamOpenTimer.Dispose();
                spamOpenTimer = null;

                // Запускаем 5-секундный таймер на закрытие
                closeSpamTimer = new System.Windows.Forms.Timer();
                closeSpamTimer.Interval = 5000; // 5 секунд
                closeSpamTimer.Tick += CloseSpamTimer_Tick;
                closeSpamTimer.Start();
            }
        }

        // --- 2. ТАЙМЕР ДЛЯ ЗАКРЫТИЯ ОШИБОК ЧЕРЕЗ 5 СЕКУНД (КЛЮЧЕВОЕ МЕСТО) ---
        private void CloseSpamTimer_Tick(object sender, EventArgs e)
        {
            closeSpamTimer.Stop();
            closeSpamTimer.Dispose();
            closeSpamTimer = null;

            // 1. Закрываем все немодальные окна (включая Mocking и Watchdog)
            foreach (var form in activeErrorForms)
            {
                if (form != null && !form.IsDisposed)
                {
                    form.Close();
                }
            }
            activeErrorForms.Clear();

            // 2. Показываем финальное сообщение (МОДАЛЬНОЕ). КОД БЛОКИРУЕТСЯ ЗДЕСЬ, ПОКА ПОЛЬЗОВАТЕЛЬ НЕ НАЖМЕТ "OK".
            MessageBox.Show(this, "Хаха, синего экрана не будет, я же не вирус :)", "Сброс протокола", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // --- НОВЫЙ ШАГ: УБИВАЕМ ВСЕ ИНСТАНСЫ ПРИЛОЖЕНИЯ (Выполняется сразу после нажатия "OK") ---
            string currentExeName = Process.GetCurrentProcess().ProcessName;
            Process[] runningInstances = Process.GetProcessesByName(currentExeName);

            // Сначала убиваем всех "сторожей" (другие инстансы)
            foreach (Process proc in runningInstances)
            {
                if (proc.Id != Process.GetCurrentProcess().Id)
                {
                    try { proc.Kill(); }
                    catch (Exception) { /* Игнорируем ошибки, если процесс уже закрыт */ }
                }
            }

            // 3. ПРЕКРАЩАЕМ ВЕСЬ ФОНОВЫЙ "СРАЧ" (Останавливаем Watchdog и все таймеры)
            if (taskManagerCheckTimer != null)
            {
                taskManagerCheckTimer.Stop();
                taskManagerCheckTimer.Dispose();
                taskManagerCheckTimer = null;
            }

            // На всякий случай останавливаем все основные таймеры, если они были запущены снова
            if (typingTimer != null) typingTimer.Stop();
            if (searchTimer != null) searchTimer.Stop();
            if (errorTimer != null) errorTimer.Stop();

            // 4. Устанавливаем флаг и закрываем ТЕКУЩУЮ форму (этот процесс умрет последним)
            currentPrankTrigger = PrankTrigger.None;
            isClosingGracefully = true;
            this.Close(); // Повторная попытка закрыть форму, которая теперь разрешена
        }

        private void TypingTimer_Tick(object sender, EventArgs e)
        {
            // Логика запуска/остановки глюков
            if (isTypingFinished)
            {
                // Глюки после печати
                if (random.Next(100) == 0) isGlitching = true;
                if (isGlitching && random.Next(10) == 0) isGlitching = false;
            }
            else
            {
                // ГЛЮКИ ВО ВРЕМЯ ПЕЧАТИ (1/50 шанс на каждый тик)
                if (random.Next(50) == 0) isGlitching = true;
                if (isGlitching && random.Next(5) == 0) isGlitching = false;

                // Логика печати
                if (codeIndex < fullCodeToType.Length)
                {
                    currentTypedCode.Append(fullCodeToType[codeIndex]);
                    codeIndex++;
                }
                else
                {
                    isTypingFinished = true;
                    typingTimer.Interval = 500; // Замедляем таймер до мигания курсора
                    // Запускаем окно командной строки, когда печать закончена
                    LaunchCmdOutput();
                }
            }

            frameCounter++;
            this.Invalidate(); // Запрашиваем перерисовку
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            OpenRandomSearch();
        }

        private void ErrorTimer_Tick(object sender, EventArgs e)
        {
            // Запускаем МОДАЛЬНУЮ ошибку (блокирует)
            LaunchRandomBlockingError();
            // Устанавливаем следующий интервал случайным образом
            errorTimer.Interval = random.Next(2000, 6000);
        }

        // --- GDI+ Метод рисования ---
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            // 1. Рисуем сам код
            Font codeFont = new Font("Consolas", 14, FontStyle.Regular);

            // Рисуем код по строкам
            string[] lines = currentTypedCode.ToString().Split(new[] { '\n' }, StringSplitOptions.None);
            int yPos = 5; // Начинаем ближе к верхнему краю

            using (Brush greenBrush = new SolidBrush(Color.LimeGreen))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    // Рисуем ближе к левому краю (X=5)
                    g.DrawString(lines[i].TrimStart(), codeFont, greenBrush, 5, yPos);
                    yPos += 50; // УВЕЛИЧЕННЫЙ ИНТЕРВАЛ МЕЖДУ СТРОКАМИ (50px)
                }
            }

            // 2. Рисуем мигающий курсор (имитация)
            if (frameCounter % 2 == 0)
            {
                string lastLine = lines.Length > 0 ? lines[lines.Length - 1] : "";
                SizeF textSize = g.MeasureString(lastLine.TrimStart(), codeFont);

                float cursorX = 5 + textSize.Width;
                float cursorY = yPos - 50; // Корректируем Y-координату для курсора в соответствии с новым интервалом (50px)

                g.FillRectangle(new SolidBrush(Color.LimeGreen), cursorX, cursorY, 8, 20);
            }

            // 3. Эффект глюка (Glitch)
            if (isGlitching)
            {
                // Используем непрозрачные цвета для максимальной видимости
                Color glitchColor = random.Next(2) == 0 ? Color.Red : Color.Blue;
                using (Brush glitchBrush = new SolidBrush(Color.FromArgb(255, glitchColor)))
                {

                    // Рисуем 10-20 случайных, мигающих прямоугольников (искажение данных)
                    for (int i = 0; i < random.Next(10, 20); i++)
                    {
                        int x = random.Next(this.Width);
                        int y = random.Next(this.Height);
                        int w = random.Next(50, 300);
                        int h = random.Next(2, 25);

                        g.FillRectangle(glitchBrush, x, y, w, h);

                        // Добавляем эффект горизонтальной линии (screen tearing)
                        if (random.Next(3) == 0)
                        {
                            using (Brush lineBrush = new SolidBrush(Color.FromArgb(255, Color.Yellow)))
                            {
                                g.FillRectangle(lineBrush, 0, y - 1, this.Width, 1); // Тонкая горизонтальная линия через весь экран
                            }
                        }
                    }

                    // Добавляем крупные вертикальные сдвиги (сильное искажение)
                    if (frameCounter % 5 == 0)
                    {
                        using (Brush shiftBrush = new SolidBrush(Color.FromArgb(255, Color.White)))
                        {
                            int shiftY = random.Next(this.Height / 3);
                            g.FillRectangle(shiftBrush, random.Next(this.Width / 2), shiftY, 2, this.Height - shiftY);
                        }
                    }
                }
            }
        }

        // --- Метод для открытия окна Командной строки с выводом ---
        private void LaunchCmdOutput()
        {
            // Успешный вывод для CMD
            string cmdOutputSuccess =
                "echo [SAMSUNG LOGIC] Applied successfully! & " +
                "echo. & " +
                "echo Target 'iPhone' vulnerability exploited. Status: INFERIOR & " +
                "echo. & " +
                "echo C:\\SAMSUNG\\BIN>SUCCESS & " +
                "timeout /t 5 /nobreak > nul & " + // Пауза 5 секунд
                "echo Command prompt closing... & " +
                "timeout /t 1 /nobreak > nul & " +
                "exit"; // Команда закрыть окно

            try
            {
                // /k - выполняет команду и оставляет окно открытым
                Process.Start("cmd.exe", $"/k \"{cmdOutputSuccess}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске CMD: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Метод для открытия МОДАЛЬНОГО сообщения об ошибке (блокирует) ---
        private void LaunchRandomBlockingError()
        {
            // Выбираем случайное сообщение из массива
            var error = errorMessages[random.Next(errorMessages.Length)];

            // Отображаем МОДАЛЬНОЕ окно ошибки, которое будет блокировать выполнение
            MessageBox.Show(this, error.message, error.title, MessageBoxButtons.OK, error.icon);
        }

        // --- Метод для открытия СПЕЦИАЛЬНОЙ НЕМОДАЛЬНОЙ ошибки Task Manager (Теперь неблокирующая) ---
        private void LaunchMockingError()
        {
            // Используем немодальную форму, чтобы ее можно было закрыть вместе с остальным спамом
            NonModalErrorForm errorForm = new NonModalErrorForm(mockingError.title, mockingError.message, mockingError.icon);
            errorForm.Show(this);
            activeErrorForms.Add(errorForm); // Добавляем в список для закрытия
        }

        // --- Метод для открытия СПЕЦИАЛЬНОЙ НЕМОДАЛЬНОЙ ошибки Watchdog (Теперь неблокирующая) ---
        private void LaunchWatchdogError()
        {
            // Используем немодальную форму, чтобы ее можно было закрыть вместе с остальным спамом
            NonModalErrorForm errorForm = new NonModalErrorForm(watchdogError.title, watchdogError.message, watchdogError.icon);
            errorForm.Show(this);
            activeErrorForms.Add(errorForm); // Добавляем в список для закрытия
        }

        // --- Метод для открытия НЕМОДАЛЬНОГО сообщения об ошибке (для спама) ---
        private void LaunchRandomNonBlockingError()
        {
            // Выбираем случайное сообщение из массива
            var error = errorMessages[random.Next(errorMessages.Length)];

            // Создаем и показываем НЕМОДАЛЬНУЮ форму
            NonModalErrorForm errorForm = new NonModalErrorForm(error.title, error.message, error.icon);
            errorForm.Show(this); // Показываем форму
            activeErrorForms.Add(errorForm); // Добавляем в список для последующего закрытия
        }

        // --- Метод для открытия случайного поиска ---
        private void OpenRandomSearch()
        {
            int index = random.Next(searchQueries.Length);
            string selectedQuery = searchQueries[index];

            string encodedQuery = Uri.EscapeDataString(selectedQuery);
            string url = $"https://www.google.com/search?q={encodedQuery}";

            try
            {
                // Открытие URL в браузере по умолчанию
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // В этом приложении лучше просто тихонько проглотить ошибку
                // Console.WriteLine($"Ошибка при открытии браузера: {ex.Message}");
            }
        }
    }
}
/// <summary>
/// The mode name is very important, as it affects all the data, so it needs to be carefully studied and chosen to be as clear and accurate as possible!
/// </summary>
public enum GameMode
{
    None = 0,
    Classic = 1, //DON'T Remove Classic, Adventure
    Adventure = 2, //DON'T Remove Classic, Adventure

    Racing = 100,
    Parking = 101,
    Clibing = 102,
    Jumping = 103,
    TimeTrial = 104,
    TimeAttack = 105,

    All = 999
}

/// <summary>
/// User chơi level này trong hoàn cảnh nào
/// </summary>
public enum PlayType
{
    /// <summary>
    /// Chơi lần đầu
    /// </summary>
    FirstTime,
    /// <summary>
    /// Chơi lại sau khi thắng màn chơi Win Screen
    /// </summary>
    RestartAtWinScreen,
    /// <summary>
    /// Chơi lại sau khi thua màn chơi từ Lose Screen
    /// </summary>
    RestartAtLoseScreen,
    /// <summary>
    /// Chơi lại sau khi thua màn chơi từ Main Screen
    /// </summary>
    RestartAtMainScreen,
    /// <summary>
    /// Chơi lại sau khi thua màn chơi từ Setting Screen
    /// </summary>
    RestartAtSettingScreen,
    /// <summary>
    /// Tự động chơi lại sau khi thua
    /// </summary>
    RestartAuto,
    /// <summary>
    /// Tiếp tục chơi  tiếp level đang chơi dở
    /// </summary>
    Continue,
}

/// <summary>
/// User đang ở màn hình nào
/// </summary>
public enum ScreenType
{
    /// <summary>
    /// Màn hình khởi động
    /// </summary>
    SplashScreen,
    /// <summary>
    /// Màn hình chính
    /// </summary>
    MainScreen,
    /// <summary>
    /// Màn hình chính
    /// </summary>
    InGameScreen,
    /// <summary>
    /// Màn hình kết quả thắng hoặc thua (nếu chỉ có một màn hình)
    /// </summary>
    ResultScreen,
    /// <summary>
    /// Màn hình hồi sinh, người chơi thua và cho cơ hội thứ 2
    /// </summary>
    ReviveScreen,
    /// <summary>
    /// Màn hình thắng
    /// </summary>
    WinScreen,
    /// <summary>
    /// Màn hình thua
    /// </summary>
    LoseScreen,
    /// <summary>
    /// Màn hình popup thanh toán bên trong ứng dụng
    /// </summary>
    PopupInApp,
    /// <summary>
    /// Màn hình popup mở tính năng mới hoặc item mới
    /// </summary>
    PopupUnlock
}

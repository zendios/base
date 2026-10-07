import UIKit

// A class to encapsulate the haptic feedback logic.
// This keeps the code organized and follows Swift best practices.
final class HapticManager {
    // A shared instance for easy access from our C-style functions.
    static let shared = HapticManager()

    // Private init to enforce singleton pattern.
    private init() {}

    // --- Haptic Implementation Methods ---

    func playSelection() {
        if #available(iOS 10.0, *) {
            DispatchQueue.main.async {
                let generator = UISelectionFeedbackGenerator()
                generator.prepare()
                generator.selectionChanged()
            }
        }
    }

    func playNotification(type: Int) {
        if #available(iOS 10.0, *) {
            guard let feedbackType = UINotificationFeedbackGenerator.FeedbackType(rawValue: type) else { return }
            DispatchQueue.main.async {
                let generator = UINotificationFeedbackGenerator()
                generator.prepare()
                generator.notificationOccurred(feedbackType)
            }
        }
    }

    func playImpact(style: Int) {
        if #available(iOS 10.0, *) {
            guard let feedbackStyle = UIImpactFeedbackGenerator.FeedbackStyle(rawValue: style) else { return }
            DispatchQueue.main.async {
                let generator = UIImpactFeedbackGenerator(style: feedbackStyle)
                generator.prepare()
                generator.impactOccurred()
            }
        }
    }
}


// --- C-style Entry Points for Unity ---
// These functions are exposed to C# using the @_cdecl attribute.
// The name inside the attribute string is the exact name C# will use for DllImport.

@_cdecl("_playSelectionHaptic")
public func _playSelectionHaptic() {
    HapticManager.shared.playSelection()
}

@_cdecl("_playNotificationHaptic")
public func _playNotificationHaptic(type: Int) {
    HapticManager.shared.playNotification(type: type)
}

@_cdecl("_playImpactHaptic")
public func _playImpactHaptic(style: Int) {
    HapticManager.shared.playImpact(style: style)
}
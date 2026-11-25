import AppKit
import SwiftUI
import ScreenCaptureKit

// The shelf and sidebar use the same library fixture and destinations as the desktop review scene.
private struct Destinations: View {
    @State private var selected: String? = "Games"
    private let destinations = [
        ("Reader", "book"), ("Games", "gamecontroller"),
        ("Workflows", "point.3.connected.trianglepath.dotted"), ("Plugins", "puzzlepiece.extension")
    ]
    var body: some View {
        List(selection: $selected) {
            ForEach(destinations, id: \.0) { title, symbol in
                Label(title, systemImage: symbol).tag(title)
            }
        }.listStyle(.sidebar)
        .safeAreaInset(edge: .bottom, alignment: .leading) {
            Label("Appearance", systemImage: "circle.lefthalf.filled").padding()
        }
    }
}

private struct Shelf: View {
    let artwork: NSImage
    @State private var scope = "All"
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Picker("Library filter", selection: $scope) {
                ForEach(["All", "Installed", "Online", "Needs match"], id: \.self) { Text($0) }
            }.pickerStyle(.segmented).labelsHidden().fixedSize()
            HStack(alignment: .top, spacing: 16) {
                tile(Image(nsImage: artwork).resizable(), "CLANNAD", "Key")
                tile(ZStack { Rectangle().fill(.quaternary); Text("T").font(.largeTitle) },
                     "The last lighthouse beyond the northern mountains", "Studio")
            }
        }.padding().frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
    private func tile(_ cover: some View, _ title: String, _ studio: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            cover.frame(width: 148, height: 222).clipShape(RoundedRectangle(cornerRadius: 6))
            Text(title).font(.headline).lineLimit(2)
            Text(studio).font(.subheadline).foregroundStyle(.secondary)
        }.frame(width: 148, alignment: .leading)
    }
}

private final class Toolbar: NSObject, NSToolbarDelegate {
    let split: NSSplitViewController
    init(_ split: NSSplitViewController) { self.split = split }
    let ids: [NSToolbarItem.Identifier] = [.toggleSidebar, .sidebarTrackingSeparator, .flexibleSpace,
        .init("Sources"), .init("Add game"), .init("Search")]
    func toolbarAllowedItemIdentifiers(_ toolbar: NSToolbar) -> [NSToolbarItem.Identifier] { ids }
    func toolbarDefaultItemIdentifiers(_ toolbar: NSToolbar) -> [NSToolbarItem.Identifier] { ids }
    func toolbar(_ toolbar: NSToolbar, itemForItemIdentifier id: NSToolbarItem.Identifier,
                 willBeInsertedIntoToolbar flag: Bool) -> NSToolbarItem? {
        if id == .sidebarTrackingSeparator {
            return NSTrackingSeparatorToolbarItem(identifier: id, splitView: split.splitView, dividerIndex: 0)
        }
        if id.rawValue == "Search" {
            let item = NSSearchToolbarItem(itemIdentifier: id)
            item.searchField.placeholderString = "Search titles and aliases"
            return item
        }
        guard id.rawValue == "Add game" || id.rawValue == "Sources" else { return nil }
        let item = NSToolbarItem(itemIdentifier: id)
        item.label = id.rawValue
        item.image = NSImage(systemSymbolName: id.rawValue == "Sources" ? "externaldrive" : "plus",
                             accessibilityDescription: id.rawValue)
        item.isEnabled = true
        item.autovalidates = false
        return item
    }
}

private let app = NSApplication.shared
let interactive = CommandLine.arguments.contains("--show")
app.setActivationPolicy(interactive ? .regular : .accessory)
app.finishLaunching()
let output = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
private let artwork = NSImage(contentsOfFile: CommandLine.arguments[2])!
private let split = NSSplitViewController()
private let sidebar = NSSplitViewItem(sidebarWithViewController: NSHostingController(rootView: Destinations()))
sidebar.minimumThickness = 160
sidebar.maximumThickness = 320
split.addSplitViewItem(sidebar)
split.addSplitViewItem(NSSplitViewItem(viewController: NSHostingController(rootView: Shelf(artwork: artwork))))
private let window = NSWindow(contentViewController: split)
window.styleMask = [.titled, .resizable, .closable, .miniaturizable, .fullSizeContentView]
window.title = "Games"
window.subtitle = "2 games"
window.toolbarStyle = .unified
private let delegate = Toolbar(split)
private let toolbar = NSToolbar(identifier: "Library")
toolbar.delegate = delegate
toolbar.displayMode = .iconOnly
window.toolbar = toolbar
window.setContentSize(NSSize(width: 1280, height: 800))
window.center()
split.splitView.setPosition(208, ofDividerAt: 0)
if interactive {
    NotificationCenter.default.addObserver(forName: NSWindow.willCloseNotification, object: window, queue: .main) { _ in
        NSApplication.shared.terminate(nil)
    }
    window.makeKeyAndOrderFront(nil)
    app.activate()
} else {
    window.order(.below, relativeTo: 0)
}

@MainActor private func capture(_ mode: String) async throws {
    window.appearance = NSAppearance(named: mode == "light" ? .aqua : .darkAqua)
    window.displayIfNeeded()
    try await Task.sleep(for: .milliseconds(350))
    let stem = output.appendingPathComponent("library-appkit-" + mode)
    let layout = window.contentLayoutRect
    let frame = window.frame
    let metrics: [String: Any] = [
        "system": ProcessInfo.processInfo.operatingSystemVersionString, "appearance": mode,
        "keyWindow": window.isKeyWindow, "scale": window.backingScaleFactor,
        "windowWidth": frame.width, "windowHeight": frame.height,
        "toolbarHeight": frame.height - layout.maxY, "sidebarWidth": sidebar.viewController.view.frame.width,
        "toolbarItems": toolbar.items.map { $0.itemIdentifier.rawValue }
    ]
    try JSONSerialization.data(withJSONObject: metrics, options: [.prettyPrinted, .sortedKeys])
        .write(to: stem.appendingPathExtension("json"))
    do {
        let own = try await SCShareableContent.currentProcess
        guard let ownWindow = own.windows.first(where: { $0.windowID == window.windowNumber })
        else { throw CocoaError(.fileReadUnknown, userInfo: [NSLocalizedDescriptionKey: "The reference window is unavailable."]) }
        let filter = SCContentFilter(desktopIndependentWindow: ownWindow)
        let config = SCStreamConfiguration()
        config.showsCursor = false
        config.ignoreShadowsSingleWindow = true
        config.width = Int((filter.contentRect.width * CGFloat(filter.pointPixelScale)).rounded())
        config.height = Int((filter.contentRect.height * CGFloat(filter.pointPixelScale)).rounded())
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
        let file = stem.appendingPathExtension("png")
        try NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:])!.write(to: file)
        print(file.path)
    } catch {
        // The view cache preserves geometry but omits layers composited by WindowServer.
        guard let frameView = window.contentView?.superview,
              let bitmap = frameView.bitmapImageRepForCachingDisplay(in: frameView.bounds)
        else { throw error }
        frameView.cacheDisplay(in: frameView.bounds, to: bitmap)
        let fallback = stem.appendingPathExtension("view-cache.png")
        try bitmap.representation(using: .png, properties: [:])!.write(to: fallback)
        print("WindowServer: \(error.localizedDescription). Incomplete view-cache image: \(fallback.path)")
    }
}

Task { @MainActor in
    if interactive { return }
    do {
        for mode in ["light", "dark"] { try await capture(mode) }
        app.terminate(nil)
    } catch {
        FileHandle.standardError.write(Data("\(error)\n".utf8))
        exit(1)
    }
}
app.run()

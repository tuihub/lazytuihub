import AppKit

final class Canvas: NSView {
    override var isFlipped: Bool { true }
    override func draw(_ dirtyRect: NSRect) {
        NSColor.windowBackgroundColor.setFill()
        dirtyRect.fill()
    }
}

final class Specimen: NSObject, NSTableViewDataSource, NSTableViewDelegate {
    let window: NSWindow
    let root = Canvas(frame: NSRect(x: 0, y: 0, width: 1120, height: 900))
    let table = NSTableView()
    let popup = NSPopover()
    let input = NSTextField(string: "Design journal")
    let primary = NSButton(title: "Subscribe", target: nil, action: nil)
    let appearanceButton = NSButton(title: "Appearance", target: nil, action: nil)
    var measured: [(String, NSView)] = []
    var status: NSTextField!

    override init() {
        window = NSWindow(contentRect: root.frame, styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        super.init()
        window.title = "Official AppKit component baseline"
        window.contentView = root
        window.setFrameOrigin(NSPoint(x: 80, y: 80))
        label("OFFICIAL COMPONENT BASELINE · APPKIT", x: 32, y: 28, font: .systemFont(ofSize: NSFont.smallSystemFontSize), color: .secondaryLabelColor)
        label("A quieter place to read", x: 32, y: 60, font: .preferredFont(forTextStyle: .largeTitle), name: "title")
        label("A reader brings subscriptions, articles, and reading preferences together.", x: 32, y: 105, font: .preferredFont(forTextStyle: .body), name: "body")
        label("Native system controls · regular control size · current macOS appearance", x: 32, y: 136, font: .preferredFont(forTextStyle: .caption1), color: .secondaryLabelColor, name: "caption")

        let sidebar = NSScrollView(frame: NSRect(x: 32, y: 192, width: 220, height: 620))
        sidebar.drawsBackground = false
        table.addTableColumn(NSTableColumn(identifier: NSUserInterfaceItemIdentifier("navigation")))
        table.headerView = nil
        table.style = .sourceList
        table.dataSource = self
        table.delegate = self
        table.rowSizeStyle = .default
        table.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
        sidebar.documentView = table
        root.addSubview(sidebar)
        table.reloadData()
        table.selectRowIndexes(IndexSet(integer: 0), byExtendingSelection: false)
        measured.append(("navigation", table))

        label("Latest articles", x: 284, y: 198, font: .preferredFont(forTextStyle: .title2), name: "section-title")
        let refresh = NSButton(image: NSImage(systemSymbolName: "arrow.clockwise", accessibilityDescription: "Refresh feeds")!, target: self, action: #selector(refreshArticles))
        place(refresh, x: 1030, y: 192, name: "icon-button")
        let filter = NSSegmentedControl(labels: ["Unread", "All articles", "Saved"], trackingMode: .selectOne, target: nil, action: nil)
        filter.selectedSegment = 0
        place(filter, x: 284, y: 244, name: "segmented-selected")

        let group = NSBox(frame: NSRect(x: 284, y: 300, width: 800, height: 154))
        group.title = "Selected article"
        group.titlePosition = .atTop
        root.addSubview(group)
        measured.append(("group", group))
        let articleTitle = NSTextField(labelWithString: "The small details of a good reading experience")
        articleTitle.font = .preferredFont(forTextStyle: .headline)
        articleTitle.frame = NSRect(x: 16, y: 82, width: 760, height: 24)
        group.contentView!.addSubview(articleTitle)
        measured.append(("article-title", articleTitle))
        let articleCaption = NSTextField(labelWithString: "Design journal · 8 minutes ago")
        articleCaption.font = .preferredFont(forTextStyle: .caption1)
        articleCaption.textColor = .secondaryLabelColor
        articleCaption.frame = NSRect(x: 16, y: 54, width: 760, height: 20)
        group.contentView!.addSubview(articleCaption)
        let articleBody = NSTextField(wrappingLabelWithString: "Clear typography and calm surfaces keep attention on the story. This paragraph uses the system body text style.")
        articleBody.font = .preferredFont(forTextStyle: .body)
        articleBody.frame = NSRect(x: 16, y: 9, width: 760, height: 40)
        group.contentView!.addSubview(articleBody)
        measured.append(("article-body", articleBody))

        label("Controls and states", x: 284, y: 486, font: .preferredFont(forTextStyle: .title2))
        primary.keyEquivalent = "\r"
        primary.target = self
        primary.action = #selector(subscribe)
        place(primary, x: 284, y: 530, name: "primary")
        let secondary = NSButton(title: "Mark as read", target: nil, action: nil)
        place(secondary, x: 400, y: 530, name: "secondary")
        let disabled = NSButton(title: "Unavailable", target: nil, action: nil)
        disabled.isEnabled = false
        place(disabled, x: 530, y: 530, name: "disabled")
        appearanceButton.target = self
        appearanceButton.action = #selector(showPreferences)
        place(appearanceButton, x: 660, y: 530, name: "popup-trigger")

        label("Subscription name", x: 284, y: 590, font: .systemFont(ofSize: NSFont.systemFontSize))
        input.frame = NSRect(x: 284, y: 618, width: 280, height: input.intrinsicContentSize.height)
        root.addSubview(input)
        measured.append(("input", input))
        label("Article filter", x: 600, y: 590, font: .systemFont(ofSize: NSFont.systemFontSize))
        let select = NSPopUpButton(frame: .zero, pullsDown: false)
        select.addItems(withTitles: ["Unread", "All articles", "Saved"])
        select.selectItem(at: 0)
        place(select, x: 600, y: 614, name: "selector")

        let checkbox = NSButton(checkboxWithTitle: "Keep articles offline", target: nil, action: nil)
        checkbox.state = .on
        place(checkbox, x: 284, y: 680, name: "checkbox-selected")
        let toggle = NSSwitch(frame: .zero)
        toggle.state = .on
        toggle.sizeToFit()
        toggle.setFrameOrigin(NSPoint(x: 600, y: 679))
        root.addSubview(toggle)
        measured.append(("switch-selected", toggle))
        label("Show unread counts", x: 650, y: 682, font: .systemFont(ofSize: NSFont.systemFontSize))
        status = label("Updated just now", x: 284, y: 740, font: .preferredFont(forTextStyle: .caption1), color: .secondaryLabelColor)

        let preferences = NSViewController()
        preferences.view = NSView(frame: NSRect(x: 0, y: 0, width: 300, height: 150))
        let heading = NSTextField(labelWithString: "Reading preferences")
        heading.font = .preferredFont(forTextStyle: .headline)
        heading.frame = NSRect(x: 20, y: 105, width: 260, height: 24)
        preferences.view.addSubview(heading)
        let showCounts = NSButton(checkboxWithTitle: "Show unread counts", target: nil, action: nil)
        showCounts.state = .on
        showCounts.frame = NSRect(x: 20, y: 62, width: 260, height: 24)
        preferences.view.addSubview(showCounts)
        let note = NSTextField(labelWithString: "Changes apply to this device.")
        note.font = .preferredFont(forTextStyle: .body)
        note.frame = NSRect(x: 20, y: 24, width: 260, height: 24)
        preferences.view.addSubview(note)
        popup.contentViewController = preferences
        popup.behavior = .transient
        popup.animates = false
    }

    @discardableResult
    func label(_ text: String, x: CGFloat, y: CGFloat, font: NSFont, color: NSColor = .labelColor, name: String? = nil) -> NSTextField {
        let view = NSTextField(labelWithString: text)
        view.font = font
        view.textColor = color
        view.sizeToFit()
        view.setFrameOrigin(NSPoint(x: x, y: y))
        root.addSubview(view)
        if let name { measured.append((name, view)) }
        return view
    }

    func place(_ button: NSButton, x: CGFloat, y: CGFloat, name: String) {
        if name != "checkbox-selected" && !(button is NSPopUpButton) { button.bezelStyle = .rounded }
        button.sizeToFit()
        button.setFrameOrigin(NSPoint(x: x, y: y))
        root.addSubview(button)
        measured.append((name, button))
    }

    func place(_ control: NSSegmentedControl, x: CGFloat, y: CGFloat, name: String) {
        control.sizeToFit()
        control.setFrameOrigin(NSPoint(x: x, y: y))
        root.addSubview(control)
        measured.append((name, control))
    }

    func numberOfRows(in tableView: NSTableView) -> Int { 2 }
    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        let cell = NSTableCellView()
        let text = NSTextField(labelWithString: row == 0 ? "Articles" : "Preferences")
        text.font = .systemFont(ofSize: NSFont.systemFontSize)
        text.frame = NSRect(x: 8, y: 0, width: 180, height: 22)
        cell.addSubview(text)
        cell.textField = text
        return cell
    }

    @objc func refreshArticles() { status.stringValue = "Feeds refreshed" }
    @objc func subscribe() { status.stringValue = "Subscribed to \(input.stringValue)" }
    @objc func showPreferences() {
        popup.show(relativeTo: appearanceButton.bounds, of: appearanceButton, preferredEdge: .maxY)
    }
}

func settle() {
    RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.4))
}

func save(_ view: NSView, at url: URL) throws {
    view.layoutSubtreeIfNeeded()
    view.displayIfNeeded()
    let size = view.bounds.size
    let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size.width), pixelsHigh: Int(size.height),
        bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    bitmap.size = size
    view.cacheDisplay(in: view.bounds, to: bitmap)
    try bitmap.representation(using: .png, properties: [:])!.write(to: url)
}

func color(_ value: NSColor) -> [Double] {
    let rgb = value.usingColorSpace(.sRGB)!
    return [Double(rgb.redComponent), Double(rgb.greenComponent), Double(rgb.blueComponent), Double(rgb.alphaComponent)]
}

func capture(_ specimen: Specimen, theme: String, state: String, output: URL) throws {
    settle()
    let stem = "macos-\(theme)-\(state)"
    try save(specimen.root, at: output.appendingPathComponent("\(stem).png"))
    var components: [[String: Any]] = []
    specimen.root.effectiveAppearance.performAsCurrentDrawingAppearance {
        for (name, view) in specimen.measured {
            let rect = view.convert(view.bounds, to: specimen.root)
            var value: [String: Any] = ["name": name, "nativeClass": String(describing: type(of: view)),
                "bounds": ["x": rect.minX, "y": rect.minY, "width": rect.width, "height": rect.height],
                "focused": specimen.window.firstResponder === view,
                "appearance": view.effectiveAppearance.name.rawValue]
            if let control = view as? NSControl {
                value["enabled"] = control.isEnabled
                value["controlSize"] = control.controlSize.rawValue
                if let font = control.font {
                    value["font"] = ["name": font.fontName, "family": font.familyName ?? "", "size": font.pointSize,
                        "traits": font.fontDescriptor.object(forKey: .traits) ?? [:]] as [String: Any]
                }
            }
            if let text = view as? NSTextField {
                value["textColor"] = color(text.textColor ?? .labelColor)
                value["focused"] = text.currentEditor() != nil
            }
            if let button = view as? NSButton { value["state"] = button.state.rawValue }
            components.append(value)
        }
    }
    var palette: [String: [Double]] = [:]
    specimen.root.effectiveAppearance.performAsCurrentDrawingAppearance {
        palette = ["windowBackground": color(.windowBackgroundColor), "controlBackground": color(.controlBackgroundColor),
            "label": color(.labelColor), "secondaryLabel": color(.secondaryLabelColor),
            "separator": color(.separatorColor), "selectedContentBackground": color(.selectedContentBackgroundColor),
            "controlAccent": color(.controlAccentColor)]
    }
    let info: [String: Any] = ["system": "macos", "theme": theme, "state": state,
        "scope": "Native AppKit view rendering; excludes window chrome, compositor blur and shadow",
        "os": ProcessInfo.processInfo.operatingSystemVersionString,
        "coordinateUnit": "point", "imageScale": 1, "windowBackingScale": specimen.window.backingScaleFactor,
        "keyWindow": specimen.window.isKeyWindow, "tokens": palette, "components": components]
    try JSONSerialization.data(withJSONObject: info, options: [.prettyPrinted, .sortedKeys]).write(to: output.appendingPathComponent("\(stem).json"))
    print("Rendered \(stem)")
}

let application = NSApplication.shared
application.setActivationPolicy(.accessory)
application.finishLaunching()
let specimen = Specimen()
specimen.window.makeKeyAndOrderFront(nil)
application.activate(ignoringOtherApps: true)
let arguments = CommandLine.arguments
let output = URL(fileURLWithPath: arguments[1], isDirectory: true)
if arguments.contains("--interactive") {
    application.run()
} else {
    do {
        for theme in ["light", "dark"] {
            specimen.window.appearance = NSAppearance(named: theme == "dark" ? .darkAqua : .aqua)
            specimen.window.makeFirstResponder(nil)
            try capture(specimen, theme: theme, state: "default", output: output)
            specimen.window.makeFirstResponder(specimen.input)
            try capture(specimen, theme: theme, state: "focus", output: output)
            specimen.window.makeFirstResponder(nil)
            specimen.showPreferences()
            settle()
            try save(specimen.popup.contentViewController!.view, at: output.appendingPathComponent("macos-\(theme)-popover.png"))
            specimen.popup.close()
        }
        specimen.window.orderOut(nil)
    } catch {
        fputs("AppKit baseline: \(error)\n", stderr)
        exit(1)
    }
}

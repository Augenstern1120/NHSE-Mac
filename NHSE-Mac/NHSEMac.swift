import SwiftUI
import UniformTypeIdentifiers

@main
struct NHSEMacApp: App {
    var body: some Scene { WindowGroup { SaveEditorView() }.windowStyle(.titleBar) }
}

struct SaveEditorView: View {
    @State private var showingImporter = false
    @State private var selectedURL: URL?
    @State private var details = "选择已从 Switch 导出的《集合啦！动物森友会》存档文件或文件夹。"
    @State private var fileSize = "尚未载入存档"
    @State private var status = "就绪"

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Image(systemName: "leaf.fill").font(.system(size: 26)).foregroundStyle(.green)
                VStack(alignment: .leading, spacing: 3) {
                    Text("NHSE for Mac").font(.title2.bold())
                    Text("Animal Crossing: New Horizons Save Editor").font(.caption).foregroundStyle(.secondary)
                }
                Spacer()
                Button("打开存档…") { showingImporter = true }.buttonStyle(.borderedProminent)
            }.padding(22)
            Divider()
            if let url = selectedURL {
                VStack(alignment: .leading, spacing: 18) {
                    Label("已选择存档", systemImage: "doc.fill").font(.headline)
                    Text(url.path).font(.system(.callout, design: .monospaced)).textSelection(.enabled)
                    HStack { Label(fileSize, systemImage: "internaldrive"); Spacer(); Label(status, systemImage: "checkmark.circle") }.foregroundStyle(.secondary)
                    Text("Mac 版界面原型已就绪。完整存档解析与编辑功能尚未接入；当前不会修改存档。").foregroundStyle(.secondary)
                    Spacer()
                }.padding(24)
            } else {
                ContentUnavailableView("打开一个存档", systemImage: "archivebox", description: Text(details))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            Divider()
            HStack { Text("存档只在本机处理").font(.caption).foregroundStyle(.secondary); Spacer(); Text("NHSE · macOS 原生原型").font(.caption).foregroundStyle(.tertiary) }.padding(12)
        }
        .frame(minWidth: 650, minHeight: 420)
        .fileImporter(isPresented: $showingImporter, allowedContentTypes: [.item, .folder, .zip], allowsMultipleSelection: false) { result in
            switch result {
            case .success(let urls):
                guard let url = urls.first else { return }
                _ = url.startAccessingSecurityScopedResource()
                selectedURL = url
                if let values = try? url.resourceValues(forKeys: [.fileSizeKey, .isDirectoryKey]) {
                    fileSize = values.isDirectory == true ? "存档文件夹" : ByteCountFormatter.string(fromByteCount: Int64(values.fileSize ?? 0), countStyle: .file)
                }
                status = "已载入路径"
            case .failure(let error): status = "打开失败：\(error.localizedDescription)"
            }
        }
    }
}

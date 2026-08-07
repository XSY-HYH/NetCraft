import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark, oneLight } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { useTheme } from '@/hooks/use-theme';
import { useRef } from 'react';

interface CodeEditorProps {
  value: string;
  onChange?: (value: string) => void;
  language?: string;
  readOnly?: boolean;
  minHeight?: string;
  placeholder?: string;
}

/**
 * 代码编辑器：textarea 叠加在 SyntaxHighlighter 之上，支持高亮 + 可编辑。
 * 两层使用完全一致的 padding/font/line-height/white-space，确保字符对齐与滚动同步。
 * language 默认 'text'，支持 'yaml' / 'json' / 'bash' / 'typescript' 等 Prism 语言。
 * 注：纯文本/日志文件建议直接用 textarea，不要用此组件（ASCII art 与超长行易错位）。
 */
const CodeEditor: React.FC<CodeEditorProps> = ({
  value,
  onChange,
  language = 'text',
  readOnly = false,
  minHeight = '400px',
  placeholder,
}) => {
  const { isDark } = useTheme();
  const textRef = useRef<HTMLTextAreaElement>(null);
  const preRef = useRef<HTMLDivElement>(null);

  // 同步滚动：textarea → pre（pre 是 pointerEvents:none，只能单向）
  const handleScroll = () => {
    if (textRef.current && preRef.current) {
      preRef.current.scrollTop = textRef.current.scrollTop;
      preRef.current.scrollLeft = textRef.current.scrollLeft;
    }
  };

  // 公共排版参数，两层必须完全一致
  const fontSize = '13px';
  const fontFamily = 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace';
  const lineHeight = 1.5;
  const padding = '12px 16px';

  const highlighterStyle: React.CSSProperties = {
    margin: 0,
    padding,
    background: 'transparent',
    fontSize,
    fontFamily,
    lineHeight,
    minHeight,
    whiteSpace: 'pre',        // 不换行，与 textarea 一致
    overflow: 'visible',       // 让内容溢出到外层 preRef，由 preRef 的 scrollLeft/Top 控制
    tabSize: 4,
    width: 'max-content',      // 宽度随内容扩展，超长行才会真正溢出，scrollLeft 才能带动文本
    minWidth: '100%',
  };

  return (
    <div
      style={{ position: 'relative', minHeight, height: minHeight }}
      className='rounded-lg border border-default-200 bg-default-50 dark:bg-default-100/20 overflow-hidden'
    >
      {/* 高亮层：pointerEvents:none，滚动由 textarea 同步 */}
      <div
        ref={preRef}
        style={{
          position: 'absolute',
          inset: 0,
          overflow: 'auto',          // 必须 auto/scroll，scrollLeft/Top 才能程序化生效
          pointerEvents: 'none',
          scrollbarWidth: 'none',    // Firefox 隐藏滚动条
        }}
        className='code-editor-highlight-scroll'
        aria-hidden
      >
        <SyntaxHighlighter
          language={language}
          style={isDark ? oneDark : oneLight}
          customStyle={highlighterStyle}
          codeTagProps={{
            style: {
              background: 'transparent',
              padding: 0,
              fontFamily,
              fontSize,
              lineHeight,
              whiteSpace: 'pre',
              tabSize: 4,
            },
          }}
          wrapLongLines={false}
        >
          {value + (value.endsWith('\n') ? ' ' : '')}
        </SyntaxHighlighter>
      </div>

      {/* 编辑层 */}
      <textarea
        ref={textRef}
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        onScroll={handleScroll}
        readOnly={readOnly}
        placeholder={placeholder}
        spellCheck={false}
        style={{
          position: 'absolute',
          inset: 0,
          background: 'transparent',
          color: 'transparent',
          caretColor: isDark ? '#fff' : '#000',
          fontSize,
          fontFamily,
          lineHeight,
          padding,
          border: 'none',
          outline: 'none',
          resize: 'none',
          width: '100%',
          height: '100%',
          whiteSpace: 'pre',
          overflow: 'auto',
          tabSize: 4,
        }}
      />
    </div>
  );
};

export default CodeEditor;

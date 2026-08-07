//PureLayout 纯净布局 居中展示 登录改密页用 无侧边栏
export default function PureLayout ({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <div className='relative flex flex-col h-screen'>
      <main className='flex-grow w-full flex flex-col justify-center items-center'>
        {children}
      </main>
    </div>
  );
}

import { SkinViewer, IdleAnimation } from 'skinview3d';
import { useEffect, useRef } from 'react';

//SkinPreview3D 3D 皮肤预览组件 基于 skinview3d 渲染 Minecraft 人物模型
//skinUrl 皮肤 PNG 地址 model classic/slim autoRotate 自动旋转
interface Props {
  skinUrl: string;
  model?: 'slim' | 'default';
  width?: number;
  height?: number;
}

export default function SkinPreview3D ({ skinUrl, model = 'default', width = 200, height = 300 }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const viewerRef = useRef<SkinViewer | null>(null);

  useEffect(() => {
    if (!canvasRef.current) return;
    //销毁旧实例
    viewerRef.current?.dispose();
    try {
      const viewer = new SkinViewer({
        canvas: canvasRef.current,
        width,
        height,
      });
      viewer.animation = new IdleAnimation();
      viewer.autoRotate = true;
      viewer.autoRotateSpeed = 0.5;
      //slim 模型需通过 loadSkin 第二参数指定
      viewer.loadSkin(skinUrl, { model: model === 'slim' ? 'slim' : 'default' });
      viewerRef.current = viewer;
    } catch {
      //WebGL 不支持或加载失败 降级为 <img>
      viewerRef.current = null;
    }
    return () => { viewerRef.current?.dispose(); viewerRef.current = null; };
  }, [skinUrl, model, width, height]);

  return <canvas ref={canvasRef} className='mx-auto' />;
}

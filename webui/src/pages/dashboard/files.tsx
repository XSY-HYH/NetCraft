import FileManager, { type FileInfo } from '@/controllers/file_manager';
import key from '@/const/key';
import useI18n from '@/hooks/use-i18n';
import CodeEditor from '@/components/code_editor';
import { Button } from '@heroui/button';
import { Card, CardBody } from '@heroui/card';
import { Input } from '@heroui/input';
import { Modal, ModalContent, ModalHeader, ModalBody, ModalFooter } from '@heroui/modal';
import { ScrollShadow } from '@heroui/scroll-shadow';
import { Spinner } from '@heroui/spinner';
import { Table, TableHeader, TableColumn, TableBody, TableRow, TableCell } from '@heroui/table';
import { Tooltip } from '@heroui/tooltip';
import { useLocalStorage } from '@uidotdev/usehooks';
import clsx from 'clsx';
import { useCallback, useEffect, useRef, useState } from 'react';
import { FaRegFolder, FaRegFile, FaArrowUp, FaFolderPlus, FaUpload, FaDownload, FaTrashAlt, FaReply, FaCopy, FaFileCsv, FaCheckSquare, FaSquare } from 'react-icons/fa';
import { IoRefresh } from 'react-icons/io5';
import toast from 'react-hot-toast';

export default function FilesPage () {
  const { t } = useI18n();
  const [currentPath, setCurrentPath] = useState('/');
  const [rootPath, setRootPath] = useState('');
  const [entries, setEntries] = useState<FileInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  // 多选模式
  const [multiSelectMode, setMultiSelectMode] = useState(false);
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());

  const [mkdirOpen, setMkdirOpen] = useState(false);
  const [mkdirName, setMkdirName] = useState('');

  const [newFileOpen, setNewFileOpen] = useState(false);
  const [newFileName, setNewFileName] = useState('');

  const [moveOpen, setMoveOpen] = useState(false);
  const [moveTarget, setMoveTarget] = useState('');
  const [moveSource, setMoveSource] = useState('');

  const [copyOpen, setCopyOpen] = useState(false);
  const [copyTarget, setCopyTarget] = useState('');
  const [copySource, setCopySource] = useState('');

  const [uploadOpen, setUploadOpen] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);

  // 文件编辑
  const [editOpen, setEditOpen] = useState(false);
  const [editPath, setEditPath] = useState('');
  const [editContent, setEditContent] = useState('');
  const [editLoading, setEditLoading] = useState(false);
  const [editSaving, setEditSaving] = useState(false);

  const [backgroundImage] = useLocalStorage<string>(key.backgroundImage, '');
  const hasBackground = !!backgroundImage;

  const loadList = useCallback(async (path: string) => {
    setLoading(true);
    try {
      const result = await FileManager.listFiles(path);
      setRootPath(result?.rootPath ?? '');
      const list = result?.entries ?? [];
      list.sort((a, b) => {
        if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
        return a.name.localeCompare(b.name);
      });
      setEntries(list);
    } catch (e) {
      toast.error(t('webui.file.load_failed'));
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadList(currentPath);
  }, [currentPath, loadList]);

  const normalizePath = (p: string) => {
    if (!p) return '/';
    p = p.replace(/\\/g, '/').replace(/\/+$/, '');
    return p.startsWith('/') ? p : '/' + p;
  };

  const joinPath = (base: string, name: string) => {
    const b = base.replace(/\/+$/, '');
    return `${b}/${name}`;
  };

  const getParent = (path: string) => {
    const p = path.replace(/\/+$/, '');
    const idx = p.lastIndexOf('/');
    return idx <= 0 ? '/' : p.substring(0, idx);
  };

  const handleEntryClick = (entry: FileInfo) => {
    if (multiSelectMode) {
      // 多选模式下点击切换选中
      setSelectedKeys(prev => {
        const next = new Set(prev);
        const k = entry.name;
        if (next.has(k)) next.delete(k);
        else next.add(k);
        return next;
      });
      return;
    }
    if (entry.isDirectory) {
      setCurrentPath(joinPath(currentPath, entry.name));
      setSelectedKey(null);
    } else {
      // 文本文件 → 编辑
      openEditModal(entry);
    }
  };

  const openEditModal = async (entry: FileInfo) => {
    const target = joinPath(currentPath, entry.name);
    setEditPath(target);
    setEditContent('');
    setEditOpen(true);
    setEditLoading(true);
    try {
      const content = await FileManager.readFile(target);
      setEditContent(content ?? '');
    } catch (e: any) {
      // 后端返回 400 表示二进制文件
      const msg = e?.message || '';
      if (msg.includes('binary') || msg.includes('二进制')) {
        toast.error(t('webui.file.binary'));
      } else {
        toast.error(t('webui.file.read_failed'));
      }
      console.error(e);
      setEditOpen(false);
    } finally {
      setEditLoading(false);
    }
  };

  const handleSaveEdit = async () => {
    setEditSaving(true);
    try {
      await FileManager.writeFile(editPath, editContent);
      toast.success(t('webui.file.saved'));
      setEditOpen(false);
    } catch (e) {
      toast.error(t('webui.file.save_failed'));
      console.error(e);
    } finally {
      setEditSaving(false);
    }
  };

  const handleMkdir = async () => {
    const name = mkdirName.trim();
    if (!name) return;
    try {
      await FileManager.createDirectory(joinPath(currentPath, name));
      toast.success(t('webui.file.mkdir_success'));
      setMkdirOpen(false);
      setMkdirName('');
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.mkdir_failed'));
      console.error(e);
    }
  };

  const handleNewFile = async () => {
    const name = newFileName.trim();
    if (!name) return;
    const target = joinPath(currentPath, name);
    try {
      await FileManager.createFile(target);
      toast.success(t('webui.file.mkdir_success'));
      setNewFileOpen(false);
      setNewFileName('');
      loadList(currentPath);
      // 创建后直接打开编辑
      openEditModal({ name, isDirectory: false, size: 0, lastWriteTime: new Date().toISOString() });
    } catch (e) {
      toast.error(t('webui.file.mkdir_failed'));
      console.error(e);
    }
  };

  const handleDelete = async (entry: FileInfo) => {
    if (!window.confirm(t('webui.file.confirm_delete', entry.name))) return;
    const target = joinPath(currentPath, entry.name);
    try {
      await FileManager.delete(target);
      toast.success(t('webui.file.delete_success'));
      if (selectedKey === entry.name) setSelectedKey(null);
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.delete_failed'));
      console.error(e);
    }
  };

  const handleBatchDelete = async () => {
    if (selectedKeys.size === 0) {
      toast.error(t('webui.file.no_selection'));
      return;
    }
    if (!window.confirm(t('webui.file.confirm_batch_delete', String(selectedKeys.size)))) return;
    const paths = Array.from(selectedKeys).map(name => joinPath(currentPath, name));
    try {
      await FileManager.batchDelete(paths);
      toast.success(t('webui.file.delete_success'));
      setSelectedKeys(new Set());
      setMultiSelectMode(false);
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.delete_failed'));
      console.error(e);
    }
  };

  const handleBatchDownload = () => {
    if (selectedKeys.size === 0) {
      toast.error(t('webui.file.no_selection'));
      return;
    }
    const paths = Array.from(selectedKeys).map(name => joinPath(currentPath, name));
    FileManager.batchDownload(paths);
  };

  const openMoveModal = (entry: FileInfo) => {
    setMoveSource(joinPath(currentPath, entry.name));
    setMoveTarget(joinPath(currentPath, entry.name));
    setMoveOpen(true);
  };

  const handleMove = async () => {
    const target = normalizePath(moveTarget);
    if (!target || target === moveSource) {
      setMoveOpen(false);
      return;
    }
    try {
      await FileManager.move(moveSource, target);
      toast.success(t('webui.file.move_success'));
      setMoveOpen(false);
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.move_failed'));
      console.error(e);
    }
  };

  const openCopyModal = (entry: FileInfo) => {
    setCopySource(joinPath(currentPath, entry.name));
    setCopyTarget(joinPath(currentPath, entry.name));
    setCopyOpen(true);
  };

  const handleCopy = async () => {
    const target = normalizePath(copyTarget);
    if (!target || target === copySource) {
      setCopyOpen(false);
      return;
    }
    try {
      await FileManager.copy(copySource, target);
      toast.success(t('webui.file.copy_success'));
      setCopyOpen(false);
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.copy_failed'));
      console.error(e);
    }
  };

  const handleDownload = (entry: FileInfo) => {
    const target = joinPath(currentPath, entry.name);
    FileManager.download(target);
  };

  const handleUploadClick = () => {
    if (fileInputRef.current) fileInputRef.current.value = '';
    setUploadOpen(true);
  };

  const handleUploadFile = async (file: File) => {
    setUploading(true);
    try {
      await FileManager.upload(currentPath, [file]);
      toast.success(t('webui.file.upload_success'));
      loadList(currentPath);
    } catch (e) {
      toast.error(t('webui.file.upload_failed'));
      console.error(e);
    } finally {
      setUploading(false);
    }
  };

  const toggleMultiSelect = () => {
    setMultiSelectMode(prev => {
      const next = !prev;
      if (!next) setSelectedKeys(new Set());
      return next;
    });
  };

  const formatSize = (bytes: number, isDir: boolean) => {
    if (isDir) return '-';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
  };

  const formatDate = (iso: string) => {
    if (!iso) return '-';
    try {
      const d = new Date(iso);
      return d.toLocaleString();
    } catch {
      return iso;
    }
  };

  const breadcrumbSegments = () => {
    const p = currentPath.replace(/^\/+|\/+$/g, '');
    if (!p) return [];
    const parts = p.split('/').filter(Boolean);
    const segs: { name: string; path: string; }[] = [];
    let acc = '';
    for (const part of parts) {
      acc = `${acc}/${part}`;
      segs.push({ name: part, path: acc });
    }
    return segs;
  };

  const cardClass = clsx(
    'backdrop-blur-sm border shadow-sm rounded-2xl',
    hasBackground ? 'bg-white/20 dark:bg-black/10 border-white/40 dark:border-white/10' : 'bg-white/60 dark:bg-black/40 border-white/40 dark:border-white/10'
  );

  return (
    <section className='w-full max-w-[1200px] mx-auto py-4 md:py-8 px-2 md:px-6 relative'>
      <title>{t('webui.file.title')}</title>

      <Card className={cardClass}>
        <CardBody className='p-4'>
          <div className='flex items-center justify-between mb-3 flex-wrap gap-2'>
            <div className='flex items-center gap-2 flex-wrap min-w-0'>
              <Button size='sm' variant='flat' isDisabled={currentPath === '/'} onPress={() => { setCurrentPath(getParent(currentPath)); setSelectedKey(null); }} startContent={<FaArrowUp />}>
                {t('webui.file.parent')}
              </Button>
              <div className='text-sm text-default-500 truncate'>
                <span className='text-default-400'>{t('webui.file.current_path')}: </span>
                <button className='hover:underline' onClick={() => { setCurrentPath('/'); setSelectedKey(null); }}>/</button>
                {breadcrumbSegments().map(seg => (
                  <span key={seg.path}>
                    <button className='hover:underline' onClick={() => { setCurrentPath(seg.path); setSelectedKey(null); }}>{seg.name}</button>
                    <span className='text-default-300'>/</span>
                  </span>
                ))}
              </div>
            </div>
            <div className='flex items-center gap-2 flex-wrap'>
              <Button size='sm' color={multiSelectMode ? 'success' : 'primary'} variant='flat' onPress={toggleMultiSelect} startContent={multiSelectMode ? <FaCheckSquare /> : <FaSquare />}>
                {multiSelectMode ? t('webui.file.exit_multi_select') : t('webui.file.multi_select')}
              </Button>
              {multiSelectMode && (
                <>
                  <Button size='sm' color='danger' variant='flat' onPress={handleBatchDelete} startContent={<FaTrashAlt />}>
                    {t('webui.file.batch_delete')} ({selectedKeys.size})
                  </Button>
                  <Button size='sm' color='primary' variant='flat' onPress={handleBatchDownload} startContent={<FaDownload />}>
                    {t('webui.file.batch_download')}
                  </Button>
                </>
              )}
              {!multiSelectMode && (
                <>
                  <Button size='sm' color='primary' variant='flat' onPress={() => { setNewFileName(''); setNewFileOpen(true); }} startContent={<FaFileCsv />}>
                    {t('webui.file.new_file')}
                  </Button>
                  <Button size='sm' color='primary' variant='flat' onPress={() => { setMkdirName(''); setMkdirOpen(true); }} startContent={<FaFolderPlus />}>
                    {t('webui.file.mkdir')}
                  </Button>
                  <Button size='sm' color='primary' variant='flat' onPress={handleUploadClick} startContent={<FaUpload />}>
                    {t('webui.file.upload')}
                  </Button>
                </>
              )}
              <Button size='sm' variant='flat' onPress={() => loadList(currentPath)} startContent={<IoRefresh />}>
                {t('webui.file.refresh')}
              </Button>
            </div>
          </div>

          {rootPath && (
            <div className='text-xs text-default-400 mb-2'>{rootPath}</div>
          )}

          {loading ? (
            <div className='flex items-center justify-center h-[300px]'>
              <Spinner />
            </div>
          ) : entries.length === 0 ? (
            <div className='flex items-center justify-center h-[300px] text-default-400'>{t('webui.file.empty')}</div>
          ) : (
            <ScrollShadow className='max-h-[600px]'>
              <Table aria-label={t('webui.file.heading')} isCompact removeWrapper selectionMode={multiSelectMode ? 'multiple' : 'single'} selectedKeys={multiSelectMode ? selectedKeys : (selectedKey ? new Set([selectedKey]) : new Set())} onSelectionChange={(keys) => {
                if (multiSelectMode) {
                  // HeroUI Table 全选时 keys 为 "all" 字符串，需特殊处理
                  if (keys === 'all') {
                    setSelectedKeys(new Set(entries.map(e => e.name)));
                  } else {
                    setSelectedKeys(new Set(Array.from(keys as Iterable<string>).map(k => String(k))));
                  }
                } else {
                  if (keys === 'all') {
                    setSelectedKey(null);
                  } else {
                    const s = Array.from(keys as Iterable<string>)[0];
                    setSelectedKey(s || null);
                  }
                }
              }}>
                <TableHeader>
                  <TableColumn>{t('webui.file.name')}</TableColumn>
                  <TableColumn>{t('webui.file.size')}</TableColumn>
                  <TableColumn>{t('webui.file.modified')}</TableColumn>
                  <TableColumn align='end'>{t('webui.file.download')}</TableColumn>
                </TableHeader>
                <TableBody>
                  {entries.map((entry) => (
                    <TableRow key={entry.name}>
                      <TableCell>
                        <div className='flex items-center gap-2 cursor-pointer' onClick={() => handleEntryClick(entry)}>
                          {entry.isDirectory ? <FaRegFolder className='text-warning-500' /> : <FaRegFile className='text-default-400' />}
                          <span className={clsx('text-sm', entry.isDirectory ? 'text-primary-500 hover:underline' : 'hover:underline')}>{entry.name}</span>
                        </div>
                      </TableCell>
                      <TableCell><span className='text-xs text-default-500'>{formatSize(entry.size, entry.isDirectory)}</span></TableCell>
                      <TableCell><span className='text-xs text-default-500'>{formatDate(entry.lastWriteTime)}</span></TableCell>
                      <TableCell>
                        <div className='flex items-center justify-end gap-1'>
                          {!entry.isDirectory && (
                            <Tooltip content={t('webui.file.download')}>
                              <Button isIconOnly size='sm' variant='light' onPress={() => handleDownload(entry)}><FaDownload /></Button>
                            </Tooltip>
                          )}
                          {!multiSelectMode && (
                            <>
                              <Tooltip content={t('webui.file.move')}>
                                <Button isIconOnly size='sm' variant='light' onPress={() => openMoveModal(entry)}><FaReply /></Button>
                              </Tooltip>
                              <Tooltip content={t('webui.file.copy')}>
                                <Button isIconOnly size='sm' variant='light' onPress={() => openCopyModal(entry)}><FaCopy /></Button>
                              </Tooltip>
                              <Tooltip content={t('webui.file.delete')} color='danger'>
                                <Button isIconOnly size='sm' variant='light' color='danger' onPress={() => handleDelete(entry)}><FaTrashAlt /></Button>
                              </Tooltip>
                            </>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </ScrollShadow>
          )}
        </CardBody>
      </Card>

      <input
        ref={fileInputRef}
        type='file'
        className='hidden'
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) handleUploadFile(f);
        }}
      />

      <Modal isOpen={mkdirOpen} onClose={() => setMkdirOpen(false)} size='sm'>
        <ModalContent>
          <ModalHeader>{t('webui.file.mkdir')}</ModalHeader>
          <ModalBody>
            <Input value={mkdirName} onValueChange={setMkdirName} placeholder={t('webui.file.name')} autoFocus />
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setMkdirOpen(false)}>{t('webui.file.cancel') || '取消'}</Button>
            <Button color='primary' onPress={handleMkdir} isDisabled={!mkdirName.trim()}>{t('webui.file.confirm') || '确定'}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={newFileOpen} onClose={() => setNewFileOpen(false)} size='sm'>
        <ModalContent>
          <ModalHeader>{t('webui.file.new_file')}</ModalHeader>
          <ModalBody>
            <Input value={newFileName} onValueChange={setNewFileName} placeholder={t('webui.file.name')} autoFocus />
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setNewFileOpen(false)}>{t('webui.file.cancel') || '取消'}</Button>
            <Button color='primary' onPress={handleNewFile} isDisabled={!newFileName.trim()}>{t('webui.file.confirm') || '确定'}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={editOpen} onClose={() => setEditOpen(false)} size='5xl'>
        <ModalContent>
          <ModalHeader>{t('webui.file.edit')} - {editPath.split('/').pop()}</ModalHeader>
          <ModalBody>
            <div className='text-xs text-default-400 mb-1'>{editPath}</div>
            {editLoading ? (
              <div className='flex items-center justify-center h-[400px]'><Spinner /></div>
            ) : (() => {
              const ext = editPath.split('.').pop()?.toLowerCase() || '';
              // 纯文本文件用普通 textarea（无高亮，避免超长行/ASCII art 错位）
              const plainExts = ['txt', 'cfg', 'ini', 'conf', 'env'];
              if (plainExts.includes(ext)) {
                return (
                  <textarea
                    value={editContent}
                    onChange={(e) => setEditContent(e.target.value)}
                    className='w-full h-[400px] p-3 rounded-lg border border-default-200 bg-default-50 dark:bg-default-100/20 font-mono text-sm resize-y outline-none focus:border-primary'
                    spellCheck={false}
                  />
                );
              }
              const langMap: Record<string, string> = {
                yml: 'yaml', yaml: 'yaml', json: 'json',
                ts: 'typescript', tsx: 'typescript', js: 'javascript', jsx: 'javascript',
                cs: 'csharp', py: 'python', sh: 'bash', bash: 'bash',
                md: 'markdown', xml: 'markup', html: 'markup', css: 'css', sql: 'sql',
                // 日志用 bash：对数字时间戳、字符串、# 注释有合理着色
                log: 'bash',
              };
              return (
                <CodeEditor
                  value={editContent}
                  onChange={setEditContent}
                  language={langMap[ext] || 'text'}
                  minHeight='400px'
                />
              );
            })()}
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setEditOpen(false)}>{t('webui.file.close') || '关闭'}</Button>
            <Button color='primary' onPress={handleSaveEdit} isLoading={editSaving} isDisabled={editLoading}>{t('webui.file.save')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={moveOpen} onClose={() => setMoveOpen(false)} size='md'>
        <ModalContent>
          <ModalHeader>{t('webui.file.move')}</ModalHeader>
          <ModalBody>
            <div className='text-xs text-default-400 mb-2'>{moveSource}</div>
            <Input value={moveTarget} onValueChange={setMoveTarget} placeholder={t('webui.file.move')} autoFocus />
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setMoveOpen(false)}>{t('webui.file.cancel') || '取消'}</Button>
            <Button color='primary' onPress={handleMove}>{t('webui.file.confirm') || '确定'}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={copyOpen} onClose={() => setCopyOpen(false)} size='md'>
        <ModalContent>
          <ModalHeader>{t('webui.file.copy')}</ModalHeader>
          <ModalBody>
            <div className='text-xs text-default-400 mb-2'>{copySource}</div>
            <Input value={copyTarget} onValueChange={setCopyTarget} placeholder={t('webui.file.copy')} autoFocus />
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setCopyOpen(false)}>{t('webui.file.cancel') || '取消'}</Button>
            <Button color='primary' onPress={handleCopy}>{t('webui.file.confirm') || '确定'}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={uploadOpen} onClose={() => setUploadOpen(false)} size='sm'>
        <ModalContent>
          <ModalHeader>{t('webui.file.upload')}</ModalHeader>
          <ModalBody>
            <div className='text-sm text-default-500 mb-2'>{t('webui.file.current_path')}: {currentPath}</div>
            <Button color='primary' variant='flat' onPress={() => fileInputRef.current?.click()} isLoading={uploading}>
              {t('webui.file.upload')}
            </Button>
          </ModalBody>
          <ModalFooter>
            <Button variant='light' onPress={() => setUploadOpen(false)}>{t('webui.file.close') || '关闭'}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </section>
  );
}

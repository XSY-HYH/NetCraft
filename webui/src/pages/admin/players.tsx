import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { Modal, ModalBody, ModalContent, ModalFooter, ModalHeader, useDisclosure } from '@heroui/modal';
import { Spinner } from '@heroui/spinner';
import { Switch } from '@heroui/switch';
import { Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/table';
import { useEffect, useRef, useState } from 'react';
import { toast } from 'react-hot-toast';
import { LuImage, LuKey, LuPlus, LuTrash, LuUser } from 'react-icons/lu';

import { Player, TpgaError, tpgaApi } from '@/api/tpga';
import useDialog from '@/hooks/use-dialog';
import useI18n from '@/hooks/use-i18n';

//PlayersPage 游戏玩家管理 增删启停 改密改ID 皮肤预览上传改类型
export default function PlayersPage () {
  const { t, tError } = useI18n();
  const dialog = useDialog();
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);
  const addModal = useDisclosure();
  const editModal = useDisclosure();
  const pwdModal = useDisclosure();
  const skinModal = useDisclosure();
  const [addUser, setAddUser] = useState('');
  const [addPwd, setAddPwd] = useState('');
  const [addEmail, setAddEmail] = useState('');
  const [editTarget, setEditTarget] = useState<Player | null>(null);
  const [editUser, setEditUser] = useState('');
  const [editUuid, setEditUuid] = useState('');
  const [editEmail, setEditEmail] = useState('');
  const [pwdTarget, setPwdTarget] = useState<Player | null>(null);
  const [newPwd, setNewPwd] = useState('');
  const [skinTarget, setSkinTarget] = useState<Player | null>(null);
  const [skinUrl, setSkinUrl] = useState('');
  const [skinLoaded, setSkinLoaded] = useState(false);
  const [skinError, setSkinError] = useState(false);
  const [uploadModel, setUploadModel] = useState<'default' | 'slim'>('default');
  const fileRef = useRef<HTMLInputElement>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await tpgaApi.listPlayers();
      setPlayers(r.players);
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  const onAdd = async () => {
    if (!addUser || !addPwd) { toast.error(t('webui.players.username') + '/' + t('webui.login.password')); return; }
    try {
      await tpgaApi.addPlayer(addUser, addPwd);
      addModal.onClose();
      setAddUser('');
      setAddPwd('');
      load();
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  const onDelete = (p: Player) => {
    dialog.confirm({
      title: t('webui.common.confirm'),
      content: t('webui.players.delete_confirm'),
      onConfirm: async () => {
        try { await tpgaApi.deletePlayer(p.id); load(); } catch (e) { toast.error(tError((e as TpgaError).error, t('webui.common.error'))); }
      },
    });
  };

  const onToggle = async (p: Player, enabled: boolean) => {
    try { await tpgaApi.togglePlayerEnabled(p.id, enabled); load(); } catch (e) { toast.error(tError((e as TpgaError).error, t('webui.common.error'))); }
  };

  const openEdit = (p: Player) => {
    setEditTarget(p);
    setEditUser(p.username);
    setEditUuid(p.uuid);
    setEditEmail(p.email ?? '');
    editModal.onOpen();
  };

  const onEdit = async () => {
    if (!editTarget) return;
    try {
      await tpgaApi.updatePlayer(editTarget.id, { username: editUser, uuid: editUuid });
      editModal.onClose();
      load();
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  const openPwd = (p: Player) => {
    setPwdTarget(p);
    setNewPwd('');
    pwdModal.onOpen();
  };

  const onChangePwd = async () => {
    if (!pwdTarget || !newPwd) return;
    try {
      await tpgaApi.changePlayerPassword(pwdTarget.id, newPwd);
      pwdModal.onClose();
      toast.success(t('webui.common.save'));
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  //刷新皮肤预览 加时间戳避免缓存
  const refreshSkin = (id: number) => {
    setSkinLoaded(false);
    setSkinError(false);
    setSkinUrl(tpgaApi.getPlayerSkinUrl(id) + '?t=' + Date.now());
  };

  const openSkin = (p: Player) => {
    setSkinTarget(p);
    setUploadModel('default');
    refreshSkin(p.id);
    skinModal.onOpen();
  };

  const onSkinLoad = () => { setSkinLoaded(true); setSkinError(false); };
  const onSkinError = () => { setSkinError(true); setSkinLoaded(true); };

  const onUpload = async () => {
    if (!skinTarget || !fileRef.current?.files?.[0]) return;
    const file = fileRef.current.files[0];
    const buf = await file.arrayBuffer();
    try {
      await tpgaApi.uploadPlayerSkin(skinTarget.id, buf, uploadModel);
      toast.success(t('webui.common.save'));
      refreshSkin(skinTarget.id);
      if (fileRef.current) fileRef.current.value = '';
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  const onChangeModel = async (model: 'default' | 'slim') => {
    if (!skinTarget) return;
    try {
      await tpgaApi.changePlayerSkinModel(skinTarget.id, model);
      toast.success(t('webui.common.save'));
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  const onDeleteSkin = () => {
    if (!skinTarget) return;
    dialog.confirm({
      title: t('webui.common.confirm'),
      content: t('webui.players.skin_delete'),
      onConfirm: async () => {
        try {
          await tpgaApi.deletePlayerSkin(skinTarget.id);
          setSkinError(true);
          toast.success(t('webui.common.save'));
        } catch (e) {
          toast.error(tError((e as TpgaError).error, t('webui.common.error')));
        }
      },
    });
  };

  return (
    <div className='flex flex-col gap-4 p-4'>
      <Card>
        <CardHeader className='flex items-center justify-between'>
          <span className='text-xl font-bold'>{t('webui.players.title')}</span>
          <Button color='primary' size='sm' startContent={<LuPlus size={16} />} onPress={addModal.onOpen}>
            {t('webui.players.add')}
          </Button>
        </CardHeader>
        <CardBody>
          {loading ? (
            <div className='flex justify-center py-10'><Spinner /></div>
          ) : players.length === 0 ? (
            <div className='text-center py-10 text-default-400'>{t('webui.players.empty')}</div>
          ) : (
            <Table aria-label='players' isStriped>
              <TableHeader>
                <TableColumn>{t('webui.players.username')}</TableColumn>
                <TableColumn>{t('webui.players.email')}</TableColumn>
                <TableColumn>{t('webui.players.uuid')}</TableColumn>
                <TableColumn>{t('webui.players.enabled')}</TableColumn>
                <TableColumn>{t('webui.common.actions')}</TableColumn>
              </TableHeader>
              <TableBody>
                {players.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{p.username}</TableCell>
                    <TableCell className='text-small'>{p.email ?? '-'}</TableCell>
                    <TableCell className='font-mono text-small'>{p.uuid}</TableCell>
                    <TableCell>
                      <Switch isSelected={p.enabled} onValueChange={(v) => onToggle(p, v)} />
                    </TableCell>
                    <TableCell>
                      <div className='flex gap-2'>
                        <Button isIconOnly size='sm' variant='flat' onPress={() => openEdit(p)} title={t('webui.players.change_id')}>
                          <LuUser size={16} />
                        </Button>
                        <Button isIconOnly size='sm' variant='flat' onPress={() => openPwd(p)} title={t('webui.players.change_password')}>
                          <LuKey size={16} />
                        </Button>
                        <Button isIconOnly size='sm' variant='flat' onPress={() => openSkin(p)} title={t('webui.players.skin_preview')}>
                          <LuImage size={16} />
                        </Button>
                        <Button isIconOnly size='sm' color='danger' variant='flat' onPress={() => onDelete(p)}>
                          <LuTrash size={16} />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardBody>
      </Card>

      <Modal isOpen={addModal.isOpen} onClose={addModal.onClose}>
        <ModalContent>
          <ModalHeader>{t('webui.players.add_title')}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <Input label={t('webui.players.username')} value={addUser} onChange={(e) => setAddUser(e.target.value)} />
            <Input label={t('webui.players.email')} value={addEmail} onChange={(e) => setAddEmail(e.target.value)} placeholder={t('webui.players.email_hint')} />
            <Input type='password' label={t('webui.login.password')} value={addPwd} onChange={(e) => setAddPwd(e.target.value)} />
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={addModal.onClose}>{t('webui.common.cancel')}</Button>
            <Button color='primary' onPress={onAdd}>{t('webui.common.add')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={editModal.isOpen} onClose={editModal.onClose}>
        <ModalContent>
          <ModalHeader>{t('webui.players.change_id')} - {editTarget?.username}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <Input label={t('webui.players.new_username')} value={editUser} onChange={(e) => setEditUser(e.target.value)} />
            <Input label={t('webui.players.email')} value={editEmail} onChange={(e) => setEditEmail(e.target.value)} placeholder={t('webui.players.email_hint')} />
            <Input label={t('webui.players.new_uuid')} value={editUuid} onChange={(e) => setEditUuid(e.target.value)} />
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={editModal.onClose}>{t('webui.common.cancel')}</Button>
            <Button color='primary' onPress={onEdit}>{t('webui.common.save')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={pwdModal.isOpen} onClose={pwdModal.onClose}>
        <ModalContent>
          <ModalHeader>{t('webui.players.change_password')} - {pwdTarget?.username}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <Input type='password' label={t('webui.password.new')} value={newPwd} onChange={(e) => setNewPwd(e.target.value)} />
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={pwdModal.onClose}>{t('webui.common.cancel')}</Button>
            <Button color='primary' onPress={onChangePwd}>{t('webui.common.save')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={skinModal.isOpen} onClose={skinModal.onClose} size='lg'>
        <ModalContent>
          <ModalHeader>{t('webui.players.skin_preview')} - {skinTarget?.username}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <div className='flex justify-center'>
              {!skinLoaded && <Spinner />}
              {skinLoaded && skinError && (
                <div className='flex items-center justify-center w-32 h-32 border rounded text-default-400 text-small'>
                  {t('webui.players.skin_empty')}
                </div>
              )}
              <img
                src={skinUrl}
                onLoad={onSkinLoad}
                onError={onSkinError}
                alt='skin'
                className={`w-32 h-32 object-contain border rounded ${skinLoaded && !skinError ? '' : 'hidden'}`}
              />
            </div>
            <div className='flex flex-col gap-2'>
              <input ref={fileRef} type='file' accept='image/png' className='text-small' />
              <div className='flex gap-2 items-center'>
                <div className='flex gap-1'>
                  <Button size='sm' color={uploadModel === 'default' ? 'primary' : 'default'} variant={uploadModel === 'default' ? 'solid' : 'flat'} onPress={() => setUploadModel('default')}>
                    {t('webui.players.model_default')}
                  </Button>
                  <Button size='sm' color={uploadModel === 'slim' ? 'primary' : 'default'} variant={uploadModel === 'slim' ? 'solid' : 'flat'} onPress={() => setUploadModel('slim')}>
                    {t('webui.players.model_slim')}
                  </Button>
                </div>
                <Button color='primary' onPress={onUpload}>{t('webui.players.skin_upload')}</Button>
              </div>
              <p className='text-tiny text-default-400'>{t('webui.players.upload_hint')}</p>
            </div>
            <div className='flex gap-2 items-center pt-2 border-t'>
              <span className='text-small text-default-500'>{t('webui.players.skin_model')}:</span>
              <Button size='sm' variant='flat' onPress={() => onChangeModel('default')}>{t('webui.players.model_default')}</Button>
              <Button size='sm' variant='flat' onPress={() => onChangeModel('slim')}>{t('webui.players.model_slim')}</Button>
              <Button size='sm' color='danger' variant='flat' onPress={onDeleteSkin} isDisabled={skinError} className='ml-auto'>
                {t('webui.players.skin_delete')}
              </Button>
            </div>
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={skinModal.onClose}>{t('webui.common.cancel')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </div>
  );
}

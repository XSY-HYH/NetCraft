import { Button } from '@heroui/button';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { Input } from '@heroui/input';
import { Modal, ModalBody, ModalContent, ModalFooter, ModalHeader, useDisclosure } from '@heroui/modal';
import { Spinner } from '@heroui/spinner';
import { Switch } from '@heroui/switch';
import { Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/table';
import { useEffect, useState } from 'react';
import { toast } from 'react-hot-toast';
import { LuKey, LuPlus, LuTrash } from 'react-icons/lu';

import { ApiAccount, TpgaError, tpgaApi } from '@/api/tpga';
import useDialog from '@/hooks/use-dialog';
import useI18n from '@/hooks/use-i18n';

//ApiAccountsPage API 账户管理 增删改密启停 wss 握手账户
export default function ApiAccountsPage () {
  const { t, tError } = useI18n();
  const dialog = useDialog();
  const [accounts, setAccounts] = useState<ApiAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const addModal = useDisclosure();
  const pwdModal = useDisclosure();
  const [addUser, setAddUser] = useState('');
  const [addPwd, setAddPwd] = useState('');
  const [pwdTarget, setPwdTarget] = useState<ApiAccount | null>(null);
  const [newPwd, setNewPwd] = useState('');

  const load = async () => {
    setLoading(true);
    try {
      const r = await tpgaApi.listApiAccounts();
      setAccounts(r.accounts);
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  const onAdd = async () => {
    if (!addUser || !addPwd) { toast.error(t('webui.api_accounts.username') + '/' + t('webui.login.password')); return; }
    try {
      await tpgaApi.addApiAccount(addUser, addPwd);
      addModal.onClose();
      setAddUser('');
      setAddPwd('');
      load();
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  const onDelete = (a: ApiAccount) => {
    dialog.confirm({
      title: t('webui.common.confirm'),
      content: t('webui.api_accounts.delete_confirm'),
      onConfirm: async () => {
        try { await tpgaApi.deleteApiAccount(a.id); load(); } catch (e) { toast.error(tError((e as TpgaError).error, t('webui.common.error'))); }
      },
    });
  };

  const onToggle = async (a: ApiAccount, enabled: boolean) => {
    try { await tpgaApi.toggleApiEnabled(a.id, enabled); load(); } catch (e) { toast.error(tError((e as TpgaError).error, t('webui.common.error'))); }
  };

  const openPwd = (a: ApiAccount) => {
    setPwdTarget(a);
    setNewPwd('');
    pwdModal.onOpen();
  };

  const onChangePwd = async () => {
    if (!pwdTarget || !newPwd) return;
    try {
      await tpgaApi.changeApiPassword(pwdTarget.id, newPwd);
      pwdModal.onClose();
      setPwdTarget(null);
      setNewPwd('');
    } catch (e) {
      toast.error(tError((e as TpgaError).error, t('webui.common.error')));
    }
  };

  return (
    <div className='flex flex-col gap-4 p-4'>
      <Card>
        <CardHeader className='flex items-center justify-between'>
          <span className='text-xl font-bold'>{t('webui.api_accounts.title')}</span>
          <Button color='primary' size='sm' startContent={<LuPlus size={16} />} onPress={addModal.onOpen}>
            {t('webui.api_accounts.add')}
          </Button>
        </CardHeader>
        <CardBody>
          {loading ? (
            <div className='flex justify-center py-10'><Spinner /></div>
          ) : accounts.length === 0 ? (
            <div className='text-center py-10 text-default-400'>{t('webui.api_accounts.empty')}</div>
          ) : (
            <Table aria-label='api-accounts' isStriped>
              <TableHeader>
                <TableColumn>{t('webui.api_accounts.username')}</TableColumn>
                <TableColumn>{t('webui.api_accounts.enabled')}</TableColumn>
                <TableColumn>{t('webui.api_accounts.created')}</TableColumn>
                <TableColumn>{t('webui.common.actions')}</TableColumn>
              </TableHeader>
              <TableBody>
                {accounts.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>{a.username}</TableCell>
                    <TableCell>
                      <Switch isSelected={a.enabled} onValueChange={(v) => onToggle(a, v)} />
                    </TableCell>
                    <TableCell>{new Date(a.createdAt).toLocaleString()}</TableCell>
                    <TableCell>
                      <div className='flex gap-2'>
                        <Button isIconOnly size='sm' variant='flat' onPress={() => openPwd(a)}>
                          <LuKey size={16} />
                        </Button>
                        <Button isIconOnly size='sm' color='danger' variant='flat' onPress={() => onDelete(a)}>
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
          <ModalHeader>{t('webui.api_accounts.add_title')}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <Input label={t('webui.api_accounts.username')} value={addUser} onChange={(e) => setAddUser(e.target.value)} />
            <Input type='password' label={t('webui.login.password')} value={addPwd} onChange={(e) => setAddPwd(e.target.value)} />
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={addModal.onClose}>{t('webui.common.cancel')}</Button>
            <Button color='primary' onPress={onAdd}>{t('webui.common.add')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <Modal isOpen={pwdModal.isOpen} onClose={pwdModal.onClose}>
        <ModalContent>
          <ModalHeader>{t('webui.api_accounts.change_pwd')} - {pwdTarget?.username}</ModalHeader>
          <ModalBody className='flex flex-col gap-4 py-4'>
            <Input type='password' label={t('webui.password.new')} value={newPwd} onChange={(e) => setNewPwd(e.target.value)} />
          </ModalBody>
          <ModalFooter>
            <Button variant='flat' onPress={pwdModal.onClose}>{t('webui.common.cancel')}</Button>
            <Button color='primary' onPress={onChangePwd}>{t('webui.common.save')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </div>
  );
}
